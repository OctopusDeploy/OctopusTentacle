using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Octopus.Shared.Contracts;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Scripts;
using Octopus.Shared.Util;

namespace Octopus.Tentacle.Services.Scripts
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
            return new Writer(logFile, fileSystem, sync, sensitiveValueMasker);
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
                    while (json.Read())
                    {
                        if (json.TokenType != JsonToken.StartArray)
                            continue;

                        sequence++;
                        if (sequence <= afterSequenceNumber)
                        {
                            continue;
                        }

                        var source = StringToSource(json.ReadAsString());
                        var message = json.ReadAsString();
                        var occurred = json.ReadAsDateTimeOffset();
                        if (occurred == null) continue;

                        results.Add(new ProcessOutput(source, message, occurred.Value));
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

        static ProcessOutputSource StringToSource(string source)
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

            public Writer(string logFile, IOctopusFileSystem fileSystem, object sync, SensitiveValueMasker sensitiveValueMasker)
            {
                this.sync = sync;
                this.sensitiveValueMasker = sensitiveValueMasker;
                writeStream = fileSystem.OpenFile(logFile, FileMode.Append, FileAccess.Write);
                writer = new StreamWriter(writeStream, Encoding.UTF8);
                json = new JsonTextWriter(writer);
            }

            public void WriteOutput(ProcessOutputSource source, string message)
            {
                lock (sync)
                {
                    json.WriteStartArray();
                    json.WriteValue(SourceToString(source));
                    json.WriteValue(MaskSensitiveValues(message));
                    json.WriteValue(DateTimeOffset.UtcNow);
                    json.WriteEndArray();
                    json.Flush();
                }
            }

            string MaskSensitiveValues(string rawMessage)
            {
                string maskedMessage = null;
                sensitiveValueMasker.SafeSanitize(rawMessage, s => maskedMessage = s);
                return maskedMessage ?? rawMessage;
            }

            public void Dispose()
            {
                json.Close();
                writer.Dispose();
                writeStream.Dispose();
            }
        }
    }
}