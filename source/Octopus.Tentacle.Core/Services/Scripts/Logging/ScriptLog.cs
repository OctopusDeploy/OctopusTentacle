using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
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
        int openWriters;

        public ScriptLog(string logFile, IOctopusFileSystem fileSystem, SensitiveValueMasker sensitiveValueMasker)
        {
            this.logFile = logFile;
            this.fileSystem = fileSystem;
            this.sensitiveValueMasker = sensitiveValueMasker;
        }

        public IScriptLogWriter CreateWriter()
        {
            // Counted only once the writer exists, so a failure to open the log file cannot leave the count
            // permanently overstating how many writers are open.
            var writer = new Writer(logFile, fileSystem, sync, sensitiveValueMasker, () => Interlocked.Decrement(ref openWriters));
            Interlocked.Increment(ref openWriters);
            return writer;
        }

        /// <summary>
        /// Structural detail about a malformed log, for diagnosing which writer produced it. Deliberately
        /// excludes surrounding log content: the log can hold unmasked fragments of sensitive values when one
        /// spans two messages, so everything here is passed through the masker before being emitted.
        /// </summary>
        string DescribeCorruption(JsonReaderException ex)
        {
            var writers = Volatile.Read(ref openWriters);
            var size = fileSystem.FileExists(logFile) ? $"{fileSystem.GetFileSize(logFile)} bytes" : "missing";
            return MaskSensitiveValues(sensitiveValueMasker,
                $"Log is {size} with {writers} open writer(s); parser reported: {ex.Message}");
        }

        static string MaskSensitiveValues(SensitiveValueMasker sensitiveValueMasker, string rawMessage)
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
            readonly object sync;
            readonly SensitiveValueMasker sensitiveValueMasker;
            readonly JsonTextWriter json;
            readonly StreamWriter writer;
            readonly Stream writeStream;
            readonly Action onDisposed;
            bool disposed;

            public Writer(string logFile, IOctopusFileSystem fileSystem, object sync, SensitiveValueMasker sensitiveValueMasker, Action onDisposed)
            {
                this.sync = sync;
                this.sensitiveValueMasker = sensitiveValueMasker;
                this.onDisposed = onDisposed;
                writeStream = fileSystem.OpenFile(logFile, FileMode.Append, FileAccess.Write);
                writer = new StreamWriter(writeStream, Encoding.UTF8);
                json = new JsonTextWriter(writer);
            }

            public void WriteOutput(ProcessOutputSource source, string message)
            => WriteOutput(source, message, DateTimeOffset.UtcNow);

            public void WriteOutput(ProcessOutputSource source, string message, DateTimeOffset occurred)
            {
                lock (sync)
                {
                    // An abandoned script keeps producing output after its runner has disposed this writer.
                    // Refusing the write here is what prevents a half-written entry reaching the log; letting it
                    // through would corrupt the file for every subsequent reader.
                    if (disposed)
                        throw new ObjectDisposedException(nameof(IScriptLogWriter),
                            "The script log writer has been disposed. The script it belongs to is no longer being tracked, so its output cannot be recorded.");

                    json.WriteStartArray();
                    json.WriteValue(SourceToString(source));
                    json.WriteValue(MaskSensitiveValues(message));
                    json.WriteValue(occurred);
                    json.WriteEndArray();
                    json.Flush();
                }
            }

            string MaskSensitiveValues(string rawMessage)
                => ScriptLog.MaskSensitiveValues(sensitiveValueMasker, rawMessage);

            public void Dispose()
            {
                // Closing under the lock keeps disposal from interleaving with an in-flight write, which would
                // otherwise flush a partial entry and leave an unbalanced token in the log.
                lock (sync)
                {
                    if (disposed) return;
                    disposed = true;

                    json.Close();
                    writer.Dispose();
                    writeStream.Dispose();
                }

                onDisposed();
            }
        }
    }
}
