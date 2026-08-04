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

        public ScriptLog(string logFile, IOctopusFileSystem fileSystem, SensitiveValueMasker sensitiveValueMasker)
        {
            this.logFile = logFile;
            this.fileSystem = fileSystem;
            this.sensitiveValueMasker = sensitiveValueMasker;
        }

        public IScriptLogWriter CreateWriter()
        {
            return new Writer(this);
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
                    while (json.Read())
                    {
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
                    // Refusing the write is what keeps a half-written entry out of the log.
                    if (disposed)
                        throw new ObjectDisposedException(nameof(IScriptLogWriter),
                            "The script log writer has been disposed. The script it belongs to is no longer being tracked, so its output cannot be recorded.");

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

                    json.Close();
                    writer.Dispose();
                    writeStream.Dispose();
                }
            }
        }
    }
}
