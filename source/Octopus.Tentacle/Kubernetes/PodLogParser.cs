using System;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Kubernetes
{
    public record PodLogLine(int LineNumber, ProcessOutputSource Source, string Message, DateTimeOffset Occurred);

    static class PodLogParser
    {
        public static (PodLogLine? LogLine, string? Error) ParseLine(string line)
        {
            var logParts = line.Split(new[] { '|' }, 4);

            if (logParts.Length != 4)
            {
                return (null, $"Invalid log line detected. '{line}' is not correctly pipe-delimited.");
            }

            if (!int.TryParse(logParts[0], out int lineNumber))
            {
                return (null, $"Invalid log line detected. '{logParts[0]}' is not a valid line number.");
            }

            if (!DateTimeOffset.TryParse(logParts[1], out var occurred))
            {
                return (null, $"Failed to parse '{logParts[1]}' as a DateTimeOffset. Using DateTimeOffset.UtcNow.");
            }

            if (!Enum.TryParse(logParts[2], true, out ProcessOutputSource source))
            {
                return (null, $"Invalid log line detected. '{logParts[2]}' is not a valid source.");
            }
            
            //add the new line
            var message = logParts[3];

            if (message.StartsWith("EOS-075CD4F0-8C76-491D-BA76-0879D35E9CFE"))
                source = ProcessOutputSource.Debug;
            
            return (new PodLogLine(lineNumber, source, message, occurred), null);
        }
    }
}