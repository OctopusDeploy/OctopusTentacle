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
            bool haveSeenPodLogEntryMatchingLogSequence = false;
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

                        //Pod log line numbers are 1-based, log sequence is 0-based
                        if (podLogLine.LineNumber > lastLogSequence)
                        {
                            //TODO: assert all lines are sequential
                            if (podLogLine.LineNumber == lastLogSequence + 1)
                                haveSeenPodLogEntryMatchingLogSequence = true;

                            if (!haveSeenPodLogEntryMatchingLogSequence)
                                throw new MissingPodLogException();

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

    class MissingPodLogException : Exception
    {
    }
}