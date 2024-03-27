using System;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Scripts;

namespace Octopus.Tentacle.Kubernetes.Scripts
{
    record LogLine(int LineNumber, ProcessOutputSource Source, string Message, DateTimeOffset Occurred);

    static class PodLogParser
    {
        public static LogLine? ParseLine(IScriptLogWriter writer, string line)
        {
            var logParts = line.Split(new[] { '|' }, 4);

            if (logParts.Length != 4)
            {
                writer.WriteOutput(ProcessOutputSource.StdErr, $"Invalid log line detected. '{line}' is not correctly pipe-delimited.");
                return null;
            }

            if (!int.TryParse(logParts[0], out int lineNumber))
            {
                writer.WriteOutput(ProcessOutputSource.StdErr, $"Invalid log line detected. '{logParts[0]}' is not a valid line number.");
                return null;
            }

            if (!DateTimeOffset.TryParse(logParts[1], out var occurred))
            {
                writer.WriteOutput(ProcessOutputSource.StdErr, $"Failed to parse '{logParts[1]}' as a DateTimeOffset. Using DateTimeOffset.UtcNow.");
                occurred = DateTimeOffset.UtcNow;
            }

            if (!Enum.TryParse(logParts[2], true, out ProcessOutputSource source))
            {
                writer.WriteOutput(ProcessOutputSource.StdErr, $"Invalid log line detected. '{logParts[2]}' is not a valid source.");
                return null;
            }
            
            //add the new line
            var message = logParts[3];

            if (message.StartsWith("EOS-075CD4F0-8C76-491D-BA76-0879D35E9CFE"))
                source = ProcessOutputSource.Debug;
            
            return new LogLine(lineNumber, source, message, occurred);
        }
    }
}