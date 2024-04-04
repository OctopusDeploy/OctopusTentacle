using System;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Kubernetes
{
    public record PodLogLine(int LineNumber, ProcessOutputSource Source, string Message, DateTimeOffset Occurred);

    public class PodLogParseResult
    {
        public bool Succeeded { get; }
        public PodLogLine? LogLine { get; }
        public string? Error { get; }

        public int? ExitCode { get; }
        
        PodLogParseResult(bool succeeded, PodLogLine? logLine, string? error, int? exitCode)
        {
            Succeeded = succeeded;
            LogLine = logLine;
            Error = error;
            ExitCode = exitCode;
        }

        public static PodLogParseResult Success(PodLogLine logLine)
        {
            return new PodLogParseResult(true, logLine, null, null);
        }
        
        public static PodLogParseResult Fail(string error)
        {
            return new PodLogParseResult(false, null, error, null);
        }
        
        public static PodLogParseResult EndOfStream(PodLogLine logLine, int exitCode)
        {
            return new PodLogParseResult(true, logLine, null, exitCode);
        }
    }

    static class PodLogLineParser
    {
        public static PodLogParseResult ParseLine(string line)
        {
            var logParts = line.Split(new[] { '|' }, 4);

            if (logParts.Length != 4)
            {
                return PodLogParseResult.Fail($"Invalid log line detected. '{line}' is not correctly pipe-delimited.");
            }

            if (!int.TryParse(logParts[0], out int lineNumber))
            {
                return PodLogParseResult.Fail($"Invalid log line detected. '{logParts[0]}' is not a valid line number.");
            }

            if (!DateTimeOffset.TryParse(logParts[1], out var occurred))
            {
                return PodLogParseResult.Fail($"Invalid log line detected. Failed to parse '{logParts[1]}' as a DateTimeOffset.");
            }

            if (!Enum.TryParse(logParts[2], true, out ProcessOutputSource source))
            {
                return PodLogParseResult.Fail($"Invalid log line detected. '{logParts[2]}' is not a valid source.");
            }
            
            //add the new line
            var message = logParts[3];

            if (message.StartsWith("EOS-075CD4F0-8C76-491D-BA76-0879D35E9CFE"))
            {
                //TODO: catch parse error
                var exitCode = int.Parse(message.Split(new[] { "<<>>" }, StringSplitOptions.None)[1]);

                return PodLogParseResult.EndOfStream(new PodLogLine(lineNumber, ProcessOutputSource.Debug, message, occurred), exitCode);
            }
            
            return PodLogParseResult.Success(new PodLogLine(lineNumber, source, message, occurred));
        }
    }
}