using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes.Scripts
{
    public class KubernetesPodOutputStreamWriter
    {
        readonly IScriptWorkspace workspace;
        long lastStdOutOffset;
        long lastStdErrOffset;

        public KubernetesPodOutputStreamWriter(IScriptWorkspace workspace)
        {
            this.workspace = workspace;
        }

        public async Task StreamPodLogsToScriptLog(IScriptLogWriter writer, CancellationToken cancellationToken, bool isFinalRead = false)
        {
            try
            {
                Stopwatch stdoutWatch = Stopwatch.StartNew();
                //open the file streams for reading
                using var stdOutStream = await SafelyOpenLogStreamReader("stdout.log", cancellationToken, writer);
                writer.WriteOutput(ProcessOutputSource.StdOut, $"Opening streams for stdout logs took: {stdoutWatch.Elapsed} (FinalRead: {isFinalRead})");

                Stopwatch stderrWatch = Stopwatch.StartNew();
                using var stdErrStream = await SafelyOpenLogStreamReader("stderr.log", cancellationToken, writer);
                writer.WriteOutput(ProcessOutputSource.StdOut, $"Opening streams for stderr logs took: {stderrWatch.Elapsed} (FinalRead: {isFinalRead})");

                //if either of these is null, just return
                if (stdOutStream is null || stdErrStream is null)
                {
                    writer.WriteOutput(ProcessOutputSource.StdOut, DateTimeOffset.UtcNow + ", " + "Cancelling stream reader open due to job completion");
                    return;
                }

                // This loop is exited when either the cancellation token is cancelled (which is when the pod is finished or the script is cancelled)
                // or if this is the final read, at the end (so we read once and jump out)
                while (true)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        writer.WriteOutput(ProcessOutputSource.StdOut, DateTimeOffset.UtcNow + ", " + "Cancelling stream writing due to job completion");
                        break;
                    }

                    Stopwatch watch = Stopwatch.StartNew();

                    //Read both the stdout and stderr log files
                    var stdOutReadTask = ReadLogFileTail(writer, stdOutStream, ProcessOutputSource.StdOut, lastStdOutOffset);
                    var stdErrReadTask = ReadLogFileTail(writer, stdErrStream, ProcessOutputSource.StdErr, lastStdErrOffset);

                    //wait for them to both complete
                    await Task.WhenAll(stdOutReadTask, stdErrReadTask);

                    //store the final offsets
                    lastStdOutOffset = stdOutReadTask.Result.FinalOffset;
                    lastStdErrOffset = stdErrReadTask.Result.FinalOffset;

                    //stitch the log lines together and order by occurred, then write to actual log
                    var orderedLogLines = stdOutReadTask.Result.Logs
                        .Concat(stdErrReadTask.Result.Logs)
                        .OrderBy(ll => ll.Occurred);

                    //write all the read log lines to the output script log
                    foreach (var logLine in orderedLogLines)
                    {
                        var logLineMessage = logLine.Message.StartsWith("##") ? logLine.Message : $"{logLine.Occurred} ({DateTimeOffset.UtcNow}), {logLine.Message}";
                        writer.WriteOutput(logLine.Source, logLineMessage, logLine.Occurred);
                    }
                    
writer.WriteOutput(ProcessOutputSource.StdOut, $"{DateTimeOffset.UtcNow}, Reading logs took: {watch.Elapsed}");
                    //wait for 250ms before reading the logs again (except on the final read)
                    if (!isFinalRead)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                    }
                    else
                    {
                        //if this is the last read we need to jump out (and not spin forever)
                        break;
                    }
                }
            }
            catch (TaskCanceledException)
            {
                //ignore all task cancelled exceptions as they may be thrown by the pod finishing (and thus signally)
                writer.WriteOutput(ProcessOutputSource.StdOut, DateTimeOffset.UtcNow + ", " + "TaskCanceledException due to job completion");
            }
        }

        async Task<StreamReader?> SafelyOpenLogStreamReader(string filename, CancellationToken cancellationToken, IScriptLogWriter writer)
        {
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    return null;

                try
                {
                    //we expect that we will receive a number of FileNotFoundException's while the pod is spinning up
                    //Eventually the file should exist
                    return new StreamReader(workspace.OpenFileStreamForReading(filename), Encoding.UTF8);
                }
                catch (FileNotFoundException ex)
                {
                    writer.WriteOutput(ProcessOutputSource.Debug, $"{DateTimeOffset.UtcNow}, FileNotFound: {filename} - {ex.Message}");

                    //wait for 50ms before reading the logs again
                    await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                }
            }
        }

        record LogLine(ProcessOutputSource Source, string Message, DateTimeOffset Occurred);

        static async Task<(IEnumerable<LogLine> Logs, long FinalOffset)> ReadLogFileTail(IScriptLogWriter writer, StreamReader reader, ProcessOutputSource source, long lastOffset)
        {
            if (reader.BaseStream.Length == lastOffset)
                return (Enumerable.Empty<LogLine>(), lastOffset);

            reader.BaseStream.Seek(lastOffset, SeekOrigin.Begin);

            var newLines = new List<LogLine>();
            string? line;
            do
            {
                line = await reader.ReadLineAsync();
                if (line.IsNullOrEmpty())
                    break;

                var logParts = line!.Split(new[] { '|' }, 2);

                if (logParts.Length != 2)
                {
                    writer.WriteOutput(ProcessOutputSource.StdErr, $"Invalid log line detected. '{line}' is not correctly pipe-delimited.");
                    continue;
                }

                //part 1 is the datetimeoffset
                if (!DateTimeOffset.TryParse(logParts[0], out var occurred))
                {
                    writer.WriteOutput(ProcessOutputSource.StdErr, $"Failed to parse '{logParts[0]}' as a DateTimeOffset. Using DateTimeOffset.UtcNow.");
                    occurred = DateTimeOffset.UtcNow;
                }

                //add the new line
                newLines.Add(new LogLine(source, logParts[1], occurred));
            } while (!line.IsNullOrEmpty());

            return (newLines, reader.BaseStream.Position);
        }
    }
}