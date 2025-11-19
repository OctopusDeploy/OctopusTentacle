using System;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Kubernetes.Crypto;

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

        public EndOfStreamPodLogLineParseResult(PodLogLine logLine, int exitCode) : base(logLine)
        {
            ExitCode = exitCode;
        }
    }

    static class PodLogLineParser
    {
        //The EOS message looks something like EOS-075CD4F0-8C76-491D-BA76-0879D35E9CFE<<>>0
        const string EndOfStreamMarkerPrefix = "EOS-075CD4F0-8C76-491D-BA76-0879D35E9CFE";
        const string EndOfStreamMarkerExitCodeDelimiter = "<<>>";

        public static PodLogLineParseResult ParseLine(string line, IPodLogEncryptionProvider encryptionProvider)
        {
            var initialParts = line.Split(new[] { '|' }, 2);
            if (initialParts.Length != 2)
            {
                return new InvalidPodLogLineParseResult($"Pod log line is not correctly pipe-delimited: '{line}'");
            }

            var datePart = initialParts[0];

            var remainingMessage = initialParts[1];

            //get the first 2 control chars and check
            var encryptionControl = remainingMessage.Substring(0, 2);

            var isEncryptedMessage = false;
            //there is an encryption control part at the start of the remaining message
            if (encryptionControl.Equals("e|", StringComparison.Ordinal))
            {
                isEncryptedMessage = true;

                //we slice the encryption control from the start of the message, then parse as normal
                remainingMessage = remainingMessage.Substring(2);
            }

            var logParts = remainingMessage.Split(new[] { '|' }, 3);
            if (logParts.Length != 3)
            {
                return new InvalidPodLogLineParseResult($"Pod log line is not correctly pipe-delimited: '{line}'");
            }

            var lineNumberPart = logParts[0];
            var outputSourcePart = logParts[1];
            var messagePart = logParts[2];

            if (!DateTimeOffset.TryParse(datePart, out var occurred))
            {
                return new InvalidPodLogLineParseResult($"Pod log timestamp '{datePart}' is invalid: '{line}'");
            }

            if (!int.TryParse(lineNumberPart, out var lineNumber))
            {
                return new InvalidPodLogLineParseResult($"Pod log line number '{lineNumberPart}' is invalid: '{line}'");
            }

            if (!Enum.TryParse(outputSourcePart, true, out ProcessOutputSource source))
            {
                return new InvalidPodLogLineParseResult($"Pod log level '{outputSourcePart}' is invalid: '{line}'");
            }

            //if the log messages are being returned from the pods encrypted, decrypt them here
            var logMessage = isEncryptedMessage
                ? encryptionProvider.Decrypt(messagePart)
                : messagePart;

            if (logMessage.StartsWith(EndOfStreamMarkerPrefix))
            {
                try
                {
                    var exitCode = int.Parse(logMessage.Split(new[] { EndOfStreamMarkerExitCodeDelimiter }, StringSplitOptions.None)[1]);
                    return new EndOfStreamPodLogLineParseResult(new PodLogLine(lineNumber, source, logMessage, occurred), exitCode);
                }
                catch (Exception)
                {
                    return new InvalidPodLogLineParseResult($"Pod log end of stream marker is invalid: '{line}'");
                }
            }

            return new ValidPodLogLineParseResult(new PodLogLine(lineNumber, source, logMessage, occurred));
        }
    }
}