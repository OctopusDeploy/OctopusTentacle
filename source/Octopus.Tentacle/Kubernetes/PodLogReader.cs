using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes
{
    static class PodLogReader
    {
        public static async Task<(IReadOnlyCollection<ProcessOutput> Lines, long NextSequenceNumber, int? exitCode)> ReadPodLogs(long lastLogSequence, StreamReader reader, string? url = null)
        {
            int? exitCode = null;
            var results = new List<ProcessOutput>();
            var nextSequenceNumber = lastLogSequence;
            long expectedLineNumber = lastLogSequence+1;
            
            bool haveReadPastPreviousBatchOfRows = false;

            List<string> allLines = new List<string>();
            while (true)
            {
                var line = await reader.ReadLineAsync();
                
                //No more to read
                if (line.IsNullOrEmpty())
                {
                    return (results, nextSequenceNumber, exitCode);
                }

                allLines.Add(line!);

                var parseResult = PodLogLineParser.ParseLine(line!);

                switch (parseResult)
                {
                    case ValidPodLogLineParseResult validParseResult:
                    {
                        var podLogLine = validParseResult.LogLine;

                        //The stream might contain lines from the last batch (so we need to ignore the older lines)
                        //Once we see a line number that's large enough,
                        //then we've read past the previous batch.
                        if (!haveReadPastPreviousBatchOfRows && podLogLine.LineNumber > lastLogSequence)
                            haveReadPastPreviousBatchOfRows = true;

                        //Pod log line numbers are 1-based, log sequence is 0-based
                        if (haveReadPastPreviousBatchOfRows)
                        {
                            //Lines must appear in order
                            if (podLogLine.LineNumber != expectedLineNumber)
                                throw new UnexpectedPodLogLineNumberException(expectedLineNumber, podLogLine.LineNumber, allLines, url);

                            expectedLineNumber++;
                            
                            if (validParseResult is EndOfStreamPodLogLineParseResult endOfStreamParseResult)
                                exitCode = endOfStreamParseResult.ExitCode;

                            results.Add(new ProcessOutput(podLogLine.Source, podLogLine.Message, podLogLine.Occurred));
                            nextSequenceNumber = podLogLine.LineNumber;
                        }

                        break;
                    }
                    case InvalidPodLogLineParseResult invalidParseResult:
                        //Unfortunately we don't have a good way to get the timestamp right to get this line to appear in the right order 
                        results.Add(new ProcessOutput(ProcessOutputSource.StdErr, invalidParseResult.Error, DateTimeOffset.UtcNow));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(parseResult), parseResult.GetType(), "Unexpected parse result type");
                }
            }
        }
    }

    class UnexpectedPodLogLineNumberException : Exception
    {
        public UnexpectedPodLogLineNumberException(long expectedLineNumber, long actualLineNumber, List<string> allLines, string? url)
        : base($"Unexpected Script Pod log line number, expected: {expectedLineNumber}, actual: {actualLineNumber}. Url: '{url}'. All lines: {string.Join("\n", allLines)}")
        {
        }
    }
}