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
        public static async Task<(IReadOnlyCollection<ProcessOutput> Lines, long NextSequenceNumber, int? exitCode)> ReadPodLogs(long lastLogSequence, StreamReader reader, ISystemLog log)
        {
            int? exitCode = null;
            var results = new List<ProcessOutput>();
            var nextSequenceNumber = lastLogSequence;
            bool haveSeenPodLogEntryMatchingLogSequence = false;
            while (true)
            {
                var line = await reader.ReadLineAsync();

                log.Verbose($"Parsing line: '{line}'");
                //No more to read
                if (line.IsNullOrEmpty())
                {
                    return (results, nextSequenceNumber, exitCode);
                }

                var parseResult = PodLogLineParser.ParseLine(line!);

                if (parseResult is ValidPodLogLineParseResult validParseResult)
                {
                    var podLogLine = validParseResult.LogLine;

                    //Pod log line numbers are 1-based, log sequence is 0-based
                    if (podLogLine.LineNumber > lastLogSequence)
                    {
                        if (podLogLine.LineNumber == lastLogSequence + 1)
                            haveSeenPodLogEntryMatchingLogSequence = true;

                        if (!haveSeenPodLogEntryMatchingLogSequence)
                            throw new MissingPodLogException();

                        if (validParseResult is EndOfStreamPodLogLineParseResult endOfStreamParseResult)
                            exitCode = endOfStreamParseResult.ExitCode;

                        results.Add(new ProcessOutput(podLogLine.Source, podLogLine.Message, podLogLine.Occurred));
                        nextSequenceNumber = podLogLine.LineNumber;
                    }
                    else
                    {
                        //TODO: print parse errors
                    }


                    //TODO: try not to read any more if we see a panic?
                }
            }
        }
    }

    class MissingPodLogException : Exception
    {
    }
}