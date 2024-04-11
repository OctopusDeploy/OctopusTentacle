using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes
{
    static class PodLogReader
    {
        public static async Task<(IReadOnlyCollection<ProcessOutput> Lines, long NextSequenceNumber, int? exitCode)> ReadPodLogs(long lastLogSequence, StreamReader reader, InMemoryTentacleScriptLog tentacleScriptLog)
        {
            int? exitCode = null;
            var results = new List<ProcessOutput>();
            var nextSequenceNumber = lastLogSequence;
            long expectedLineNumber = lastLogSequence+1;
            
            bool haveReadPastPreviousBatchOfRows = false;
            
            while (true)
            {
                var line = await reader.ReadLineAsync();

                //No more to read
                if (line.IsNullOrEmpty())
                {
                    return (results, nextSequenceNumber, exitCode);
                }

                tentacleScriptLog.Verbose("Parsing line: " + line);
                var parseResult = PodLogLineParser.ParseLine(line!);

                switch (parseResult)
                {
                    case ValidPodLogLineParseResult validParseResult:
                    {
                        var podLogLine = validParseResult.LogLine;

                        //Once we see a line number that's large enough,
                        //then we've read past the previous batch.
                        if (!haveReadPastPreviousBatchOfRows && podLogLine.LineNumber > lastLogSequence)
                            haveReadPastPreviousBatchOfRows = true;

                        //Pod log line numbers are 1-based, log sequence is 0-based
                        if (haveReadPastPreviousBatchOfRows)
                        {
                            //Lines must appear in order
                            if (podLogLine.LineNumber != expectedLineNumber)
                                throw new UnexpectedPodLogLineNumberException();

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


                //TODO: try not to read any more if we see a panic?

            }
        }
    }

    class UnexpectedPodLogLineNumberException : Exception
    {
    }
}