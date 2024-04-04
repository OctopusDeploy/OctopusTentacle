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
        public static async Task<(IReadOnlyCollection<ProcessOutput> Lines, long NextSequenceNumber, int? exitCode)> ReadPodLogs(long lastLogSequence, StreamReader reader)
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

                //TODO: print parse errors
                var parseResult = PodLogLineParser.ParseLine(line!);
                var podLogLine = parseResult.LogLine;
                
                //Pod log line numbers are 1-based, log sequence is 0-based
                if (podLogLine != null && podLogLine.LineNumber > lastLogSequence)
                {
                    if (podLogLine.LineNumber == lastLogSequence + 1)
                        haveSeenPodLogEntryMatchingLogSequence = true;

                    if (!haveSeenPodLogEntryMatchingLogSequence)
                        throw new MissingPodLogException();

                    if (parseResult.ExitCode != null)
                        exitCode = parseResult.ExitCode.Value;
                    
                    results.Add(new ProcessOutput(podLogLine.Source, podLogLine.Message, podLogLine.Occurred));
                    nextSequenceNumber = podLogLine.LineNumber;
                }

                //TODO: try not to read any more if we see a panic?
                    
            }
        }
    }

    class MissingPodLogException : Exception
    {
    }
}