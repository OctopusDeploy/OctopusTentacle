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
        public static async Task<(IReadOnlyCollection<ProcessOutput> Lines, long NextSequenceNumber)> ReadPodLogs(long lastLogSequence, StreamReader reader)
        {
            var results = new List<ProcessOutput>();
            var nextSequenceNumber = lastLogSequence;
            bool haveSeenPodLogEntryMatchingLogSequence = false;
            
            while (true)
            {
                var line = await reader.ReadLineAsync();

                //No more to read
                if (line.IsNullOrEmpty())
                {
                    return (results, nextSequenceNumber);
                }

                //TODO: print parse errors
                var parseResult = PodLogParser.ParseLine(line!);
                var podLogLine = parseResult.LogLine;
                
                //Pod log line numbers are 1-based, log sequence is 0-based
                if (podLogLine != null && podLogLine.LineNumber > lastLogSequence)
                {
                    if (podLogLine.LineNumber == lastLogSequence + 1)
                        haveSeenPodLogEntryMatchingLogSequence = true;

                    if (!haveSeenPodLogEntryMatchingLogSequence)
                        throw new MissingPodLogException();

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