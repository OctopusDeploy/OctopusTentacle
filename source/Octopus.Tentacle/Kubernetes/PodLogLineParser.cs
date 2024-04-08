using System;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Startup;
using YamlDotNet.Core.Tokens;

namespace Octopus.Tentacle.Kubernetes
{
    public record PodLogLine(int LineNumber, ProcessOutputSource Source, string Message, DateTimeOffset Occurred);

    public abstract class PodLogLineParseResult
    {
        public enum LogLineType
        {
            Valid,
            Invalid,
            EndOfStream
        }

        public abstract LogLineType Type { get; }
    }

    public class ValidPodLogLineParseResult : PodLogLineParseResult
    {
        public override LogLineType Type => LogLineType.Valid;

        public PodLogLine LogLine { get; }

        public ValidPodLogLineParseResult(PodLogLine logLine)
        {
            LogLine = logLine;
        }
    }

    public class InvalidPodLogLineParseResult : PodLogLineParseResult
    {
        public override LogLineType Type => LogLineType.Invalid;

        public string Error { get; }

        public InvalidPodLogLineParseResult(string error)
        {
            Error = error;
        }
    }
    
    public class EndOfStreamPodLogLineParseResult : ValidPodLogLineParseResult
    {
        public override LogLineType Type => LogLineType.EndOfStream;

        public int ExitCode { get; }

        public EndOfStreamPodLogLineParseResult( PodLogLine logLine, int exitCode) : base(logLine)
        {
            ExitCode = exitCode;
        }
    }

    static class PodLogLineParser
    {
        //The EOS message looks something like EOS-075CD4F0-8C76-491D-BA76-0879D35E9CFE<<>>0
        const string EndOfStreamMarkerPrefix = "EOS-075CD4F0-8C76-491D-BA76-0879D35E9CFE";
        const string EndOfStreamMarkerExitCodeDelimiter = "<<>>";

        public static PodLogLineParseResult ParseLine(string line)
        {
            var logParts = line.Split(new[] { '|' }, 4);

            if (logParts.Length != 4)
            {
                return new InvalidPodLogLineParseResult($"Invalid log line detected. '{line}' is not correctly pipe-delimited.");
            }

            if (!int.TryParse(logParts[0], out int lineNumber))
            {
                return new InvalidPodLogLineParseResult($"Invalid log line detected. '{logParts[0]}' is not a valid line number.");
            }

            if (!DateTimeOffset.TryParse(logParts[1], out var occurred))
            {
                return new InvalidPodLogLineParseResult($"Invalid log line detected. Failed to parse '{logParts[1]}' as a DateTimeOffset.");
            }

            if (!Enum.TryParse(logParts[2], true, out ProcessOutputSource source))
            {
                return new InvalidPodLogLineParseResult($"Invalid log line detected. '{logParts[2]}' is not a valid source.");
            }
            
            //add the new line
            var message = logParts[3];

            if (message.StartsWith(EndOfStreamMarkerPrefix))
            {
                try
                {
                    var exitCode = int.Parse(message.Split(new[] { EndOfStreamMarkerExitCodeDelimiter }, StringSplitOptions.None)[1]);
                    return new EndOfStreamPodLogLineParseResult(new PodLogLine(lineNumber, source, message, occurred), exitCode);
                }
                catch (Exception)
                {
                    return new InvalidPodLogLineParseResult($"Invalid log line detected. '{message}' is not a valid end of stream message.");
                }
            }
            
            return new ValidPodLogLineParseResult(new PodLogLine(lineNumber, source, message, occurred));
        }
    }
}