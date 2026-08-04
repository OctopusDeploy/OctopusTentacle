using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Core.Services.Scripts.Security.Masking;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Core.Services.Scripts.Logging
{
    public class ScriptLog : IScriptLog
    {
        readonly string logFile;
        readonly IOctopusFileSystem fileSystem;
        readonly SensitiveValueMasker sensitiveValueMasker;
        readonly object sync = new object();
        // Diagnostic state for DescribeCorruption. Deliberately per-instance: a read that happens after the script
        // leaves the tracker builds a fresh ScriptLog (ScriptServiceV2.GetResponse, ScriptService.GetResponse), so
        // these describe only the reading instance's own writers. That covers reads while the script is still
        // tracked, which is where the failure this was added for occurred, and reports defaults on later reads.
        int openWriters;
        int peakOpenWriters;
        bool writeRefusedAfterDisposal;

        public ScriptLog(string logFile, IOctopusFileSystem fileSystem, SensitiveValueMasker sensitiveValueMasker)
        {
            this.logFile = logFile;
            this.fileSystem = fileSystem;
            this.sensitiveValueMasker = sensitiveValueMasker;
        }

        public IScriptLogWriter CreateWriter()
        {
            lock (sync)
            {
                // Opened under the lock so a reader cannot observe an uncounted writer, and counted only once the
                // open has succeeded so a failure cannot leave the count overstated.
                var writer = new Writer(this);
                openWriters++;
                if (openWriters > peakOpenWriters) peakOpenWriters = openWriters;
                return writer;
            }
        }

        /// <summary>
        /// Structural detail about a malformed log, to narrow down which writer produced it. Deliberately
        /// excludes log content: the log can hold unmasked fragments of a sensitive value that spans two
        /// messages, so everything here is passed through the masker before being emitted.
        /// </summary>
        /// <remarks>
        /// The peak count matters more than the current one. Corruption is only noticed during a read, by which
        /// point the writer that caused it has usually been disposed, so the current count is nearly always
        /// zero. A peak above one means two writers were appending to this log at the same time.
        /// </remarks>
        string DescribeCorruption(JsonReaderException ex)
        {
            var size = fileSystem.FileExists(logFile) ? $"{fileSystem.GetFileSize(logFile)} bytes" : "missing";
            var refused = writeRefusedAfterDisposal ? ", a write was refused after disposal" : "";
            return MaskSensitiveValues(
                $"Log is {size}, {openWriters} writer(s) open now, peak {peakOpenWriters}{refused}; " +
                $"parser reported: {ex.Message}");
        }

        string MaskSensitiveValues(string rawMessage)
        {
            string? maskedMessage = null;
            sensitiveValueMasker.SafeSanitize(rawMessage, s => maskedMessage = s);
            return maskedMessage ?? rawMessage;
        }

        public List<ProcessOutput> GetOutput(long afterSequenceNumber, out long nextSequenceNumber)
        {
            var results = new List<ProcessOutput>();
            nextSequenceNumber = afterSequenceNumber;
            lock (sync)
            {
                using (var writer = new StreamReader(fileSystem.OpenFile(logFile, FileAccess.Read, FileShare.ReadWrite), Encoding.UTF8))
                using (var json = new JsonTextReader(writer))
                {
                    json.SupportMultipleContent = true;

                    var sequence = 0L;
                    DateTimeOffset? lastLogLineOccured = null;
                    while (true)
                    {
                        try
                        {
                            if (!json.Read()) break;
                        }
                        catch (JsonReaderException ex)
                        {
                            // The log is appended to while the script runs, so a read can land on a malformed
                            // entry. Reporting it occupies the next sequence number, which stops every later
                            // request re-reporting the same corruption forever.
                            var corruptionSequence = sequence + 1;
                            if (afterSequenceNumber < corruptionSequence)
                            {
                                results.Add(new ProcessOutput(ProcessOutputSource.StdErr,
                                    $"Corrupt Tentacle log at line {corruptionSequence}, no more logs will be read. {DescribeCorruption(ex)}",
                                    lastLogLineOccured ?? DateTimeOffset.Now));
                            }

                            sequence = corruptionSequence;
                            break;
                        }

                        if (json.TokenType != JsonToken.StartArray)
                            continue;

                        sequence++;
                        if (sequence <= afterSequenceNumber)
                        {
                            continue;
                        }

                        try
                        {
                            var source = StringToSource(json.ReadAsString());
                            var message = json.ReadAsString();
                            var occurred = json.ReadAsDateTimeOffset();
                            lastLogLineOccured = occurred;
                            if (occurred == null || message == null) continue;

                            results.Add(new ProcessOutput(source, message, occurred.Value));
                        }
                        catch (Exception)
                        {
                            results.Add(new ProcessOutput(ProcessOutputSource.StdErr, $"Corrupt Tentacle log at line {sequence}, no more logs will be read", lastLogLineOccured??DateTimeOffset.Now));
                            // Tentacle doesn't continue to write to logs after it has died so it is probably safe to assume we don't
                            // need to try to read more JSONL lines.
                            break;
                        }
                    }

                    if (sequence > nextSequenceNumber)
                    {
                        nextSequenceNumber = sequence;
                    }
                }
            }

            return results;
        }

        static string SourceToString(ProcessOutputSource source)
        {
            switch (source)
            {
                case ProcessOutputSource.StdErr:
                    return "stderr";
                case ProcessOutputSource.Debug:
                    return "debug";
                case ProcessOutputSource.StdOut:
                    return "stdout";
                default:
                    throw new NotSupportedException($"The {nameof(ProcessOutputSource)} option of '{source}' is not understood yet. Update the {nameof(ScriptLog)}.{nameof(SourceToString)} method so it can process these messages succssfully.");
            }
        }

        static ProcessOutputSource StringToSource(string? source)
        {
            switch (source)
            {
                case "stderr":
                    return ProcessOutputSource.StdErr;
                case "stdout":
                    return ProcessOutputSource.StdOut;
                case "debug":
                    return ProcessOutputSource.Debug;
                default:
                    throw new NotSupportedException($"The source '{source}' is not understood yet. Update the {nameof(ScriptLog)}.{nameof(StringToSource)} method so it can process these messages succssfully.");
            }
        }

        class Writer : IScriptLogWriter
        {
            readonly ScriptLog owner;
            readonly JsonTextWriter json;
            readonly StreamWriter writer;
            readonly Stream writeStream;
            bool disposed;

            public Writer(ScriptLog owner)
            {
                this.owner = owner;
                writeStream = owner.fileSystem.OpenFile(owner.logFile, FileMode.Append, FileAccess.Write);
                writer = new StreamWriter(writeStream, Encoding.UTF8);
                json = new JsonTextWriter(writer);
            }

            public void WriteOutput(ProcessOutputSource source, string message)
            => WriteOutput(source, message, DateTimeOffset.UtcNow);

            public void WriteOutput(ProcessOutputSource source, string message, DateTimeOffset occurred)
            {
                lock (owner.sync)
                {
                    // An abandoned script keeps producing output after its runner has disposed this writer.
                    // Refusing the write here is what prevents a half-written entry reaching the log; letting it
                    // through would corrupt the file for every subsequent reader. The refusal is recorded because
                    // a later corruption report is the only place the two facts can be seen together.
                    if (disposed)
                    {
                        owner.writeRefusedAfterDisposal = true;
                        throw new ObjectDisposedException(nameof(IScriptLogWriter),
                            "The script log writer has been disposed. The script it belongs to is no longer being tracked, so its output cannot be recorded.");
                    }

                    json.WriteStartArray();
                    json.WriteValue(SourceToString(source));
                    json.WriteValue(owner.MaskSensitiveValues(message));
                    json.WriteValue(occurred);
                    json.WriteEndArray();
                    json.Flush();
                }
            }

            public void Dispose()
            {
                // Closing under the lock keeps disposal from interleaving with an in-flight write, which would
                // otherwise flush a partial entry and leave an unbalanced token in the log.
                lock (owner.sync)
                {
                    if (disposed) return;
                    disposed = true;

                    try
                    {
                        json.Close();
                        writer.Dispose();
                        writeStream.Dispose();
                    }
                    finally
                    {
                        // Runs even when closing fails, so a failed close cannot leave the open-writer count
                        // overstated for the life of the log.
                        owner.openWriters--;
                    }
                }
            }
        }
    }
}
