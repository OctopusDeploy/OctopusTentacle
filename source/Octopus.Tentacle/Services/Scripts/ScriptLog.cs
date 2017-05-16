using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Octopus.Shared.Contracts;
using Octopus.Shared.Scripts;
using Octopus.Shared.Util;

namespace Octopus.Tentacle.Services.Scripts
{
    public class ScriptLog : IScriptLog
    {
        readonly string logFile;
        readonly IOctopusFileSystem fileSystem;
        readonly object sync = new object();

        public ScriptLog(string logFile, IOctopusFileSystem fileSystem)
        {
            this.logFile = logFile;
            this.fileSystem = fileSystem;
        }

        public IScriptLogWriter CreateWriter()
        {
            return new Writer(logFile, fileSystem, sync);
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
                default:
                    return "stdout";
            }
        }

        static ProcessOutputSource StringToSource(string source)
        {
            switch (source)
            {
                case "stderr":
                    return ProcessOutputSource.StdErr;
                default:
                    return ProcessOutputSource.StdOut;
            }
        }

        class Writer : IScriptLogWriter
        {
            readonly object sync;
            readonly JsonTextWriter json;
            readonly StreamWriter writer;
            readonly Stream writeStream;

            public Writer(string logFile, IOctopusFileSystem fileSystem, object sync)
            {
                this.sync = sync;
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
                    json.WriteValue(message);
                    json.WriteValue(DateTimeOffset.UtcNow);
                    json.WriteEndArray();
                    json.Flush();
                }
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