﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Services.Scripts
{
    public class ScriptLog : IScriptLog
    {
        readonly string logFile;
        readonly IOctopusFileSystem fileSystem;
        readonly SensitiveValueMasker sensitiveValueMasker;
        static readonly ConcurrentDictionary<string, object> SyncObjects = new();
        readonly List<ProcessOutput> logs = new();
        
        public static void ReleaseLock(string logFile)
        {
            SyncObjects.TryRemove(logFile, out object _);
        }

        public ScriptLog(string logFile, IOctopusFileSystem fileSystem, SensitiveValueMasker sensitiveValueMasker)
        {
            this.logFile = logFile;
            this.fileSystem = fileSystem;
            this.sensitiveValueMasker = sensitiveValueMasker;
            SyncObjects.TryAdd(logFile, new object());
        }

        public IScriptLogWriter CreateWriter()
        {
            return new Writer(logFile, fileSystem, SyncObjects[logFile], sensitiveValueMasker, logs);
        }

        public List<ProcessOutput> GetOutput(long afterSequenceNumber, out long nextSequenceNumber)
        {
            var results = new List<ProcessOutput>();
            nextSequenceNumber = afterSequenceNumber;
            lock (SyncObjects[logFile])
            {
                results = logs.Skip((int)afterSequenceNumber).ToList();
                nextSequenceNumber = afterSequenceNumber + results.Count;
                
                // using (var reader = new StreamReader(fileSystem.OpenFile(logFile, FileAccess.Read, FileShare.ReadWrite), Encoding.UTF8))
                // using (var json = new JsonTextReader(reader))
                // {
                //     try
                //     {
                //         json.SupportMultipleContent = true;
                //
                //         var sequence = 0L;
                //         DateTimeOffset? lastLogLineOccured = null;
                //         while (json.Read())
                //         {
                //             if (json.TokenType != JsonToken.StartArray)
                //                 continue;
                //
                //             sequence++;
                //             if (sequence <= afterSequenceNumber)
                //             {
                //                 continue;
                //             }
                //
                //             try
                //             {
                //                 var source = StringToSource(json.ReadAsString());
                //                 var message = json.ReadAsString();
                //                 var occurred = json.ReadAsDateTimeOffset();
                //                 lastLogLineOccured = occurred;
                //                 if (occurred == null || message == null) continue;
                //
                //                 results.Add(new ProcessOutput(source, message, occurred.Value));
                //             }
                //             catch (Exception)
                //             {
                //                 results.Add(new ProcessOutput(ProcessOutputSource.StdErr, $"Corrupt Tentacle log at line {sequence}, no more logs will be read", lastLogLineOccured ?? DateTimeOffset.Now));
                //                 // Tentacle doesn't continue to write to logs after it has died so it is probably safe to assume we don't
                //                 // need to try to read more JSONL lines.
                //                 break;
                //             }
                //         }
                //
                //         if (sequence > nextSequenceNumber)
                //         {
                //             nextSequenceNumber = sequence;
                //         }
                //     }
                //     catch (Exception ex)
                //     {
                //         string logText = fileSystem.ReadAllText(logFile);
                //         throw new Exception($"Logfile: '{logText}'", ex);
                //     }
                // }
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
            readonly List<ProcessOutput> processOutputs;
            // readonly JsonTextWriter json;
            // readonly StreamWriter writer;
            // readonly Stream writeStream;

            public Writer(string logFile, IOctopusFileSystem fileSystem, object sync, SensitiveValueMasker sensitiveValueMasker, List<ProcessOutput> processOutputs)
            {
                this.sync = sync;
                this.sensitiveValueMasker = sensitiveValueMasker;
                this.processOutputs = processOutputs;
                // writeStream = fileSystem.OpenFile(logFile, FileMode.Append, FileAccess.Write);
                // writer = new StreamWriter(writeStream, Encoding.UTF8);
                // json = new JsonTextWriter(writer);
            }

            public void WriteOutput(ProcessOutputSource source, string message)
            => WriteOutput(source, message, DateTimeOffset.UtcNow);

            public void WriteOutput(ProcessOutputSource source, string message, DateTimeOffset occurred)
            {
                lock (sync)
                {
                    processOutputs.Add(new ProcessOutput(source, MaskSensitiveValues(message), occurred));
                    
                    // json.WriteStartArray();
                    // json.WriteValue(SourceToString(source));
                    // json.WriteValue(MaskSensitiveValues(message));
                    // json.WriteValue(occurred);
                    // json.WriteEndArray();
                    // json.Flush();
                }
            }

            string MaskSensitiveValues(string rawMessage)
            {
                string? maskedMessage = null;
                sensitiveValueMasker.SafeSanitize(rawMessage, s => maskedMessage = s);
                return maskedMessage ?? rawMessage;
            }

            public void Dispose()
            {
                // json.Close();
                // writer.Dispose();
                // writeStream.Dispose();
            }
        }
    }
}
