using System;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Startup;
using YamlDotNet.Core.Tokens;

namespace Octopus.Tentacle.Kubernetes
{
    public record PodLogLine(long LineNumber, ProcessOutputSource Source, string Message, DateTimeOffset Occurred);

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
                return new InvalidPodLogLineParseResult($"Pod log line is not correctly pipe-delimited: '{line}'");
            }

            var datePart = logParts[0];
            var lineNumberPart = logParts[1];
            var outputSourcePart = logParts[2];
            var messagePart = logParts[3];

            if (!DateTimeOffset.TryParse(datePart, out var occurred))
            {
                return new InvalidPodLogLineParseResult($"Pod log timestamp '{datePart}' is invalid: '{line}'");
            }

            if (!int.TryParse(lineNumberPart, out int lineNumber))
            {
                return new InvalidPodLogLineParseResult($"Pod log line number '{lineNumberPart}' is invalid: '{line}'");
            }

            if (!Enum.TryParse(outputSourcePart, true, out ProcessOutputSource source))
            {
                return new InvalidPodLogLineParseResult($"Pod log level '{outputSourcePart}' is invalid: '{line}'");
            }
            
            if (messagePart.StartsWith(EndOfStreamMarkerPrefix))
            {
                try
                {
                    var exitCode = int.Parse(messagePart.Split(new[] { EndOfStreamMarkerExitCodeDelimiter }, StringSplitOptions.None)[1]);
                    return new EndOfStreamPodLogLineParseResult(new PodLogLine(lineNumber, source, messagePart, occurred), exitCode);
                }
                catch (Exception)
                {
                    return new InvalidPodLogLineParseResult($"Invalid log line detected. '{messagePart}' is not a valid end of stream message.");
                }
            }
            
            return new ValidPodLogLineParseResult(new PodLogLine(lineNumber, source, messagePart, occurred));
        }
    }
}