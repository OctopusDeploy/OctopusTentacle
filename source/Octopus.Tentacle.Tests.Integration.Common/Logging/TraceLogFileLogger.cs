using System;
using System.Text;
using System.Threading.Channels;

namespace Octopus.Tentacle.Tests.Integration.Common.Logging;

public class TraceLogFileLogger : IAsyncDisposable
{
    readonly Channel<string> channel;
    public readonly string logFilePath;
    readonly string testHash;

    readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
    readonly Task writeDataToDiskTask;

    public TraceLogFileLogger(string testHash)
    {
        channel = Channel.CreateUnbounded<string>();
        this.testHash = testHash;
        this.logFilePath = LogFilePath(testHash);
        File.Delete(logFilePath);

        writeDataToDiskTask = WriteDataToFile();
    }

    public void WriteLine(string logMessage)
    {
        if (cancellationTokenSource.IsCancellationRequested) return;
        
        channel.Writer.TryWrite(logMessage);
    }

    async Task WriteDataToFile()
    {
        while (!cancellationTokenSource.IsCancellationRequested)
        {
            var list = new List<string>();

            try
            {
                // Don't hammer the disk, let some log message queue up before writing them.
                await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationTokenSource.Token);

                while (await channel.Reader.WaitToReadAsync(cancellationTokenSource.Token))
                {
                    while (channel.Reader.TryRead(out var message))
                    {
                        list.Add(message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }

            using (var fileWriter = new FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.Delete | FileShare.ReadWrite))
            using (var fileAppender = new StreamWriter(fileWriter, Encoding.UTF8, 8192))
            {
                foreach (var logLine in list) await fileAppender.WriteLineAsync(logLine);

                await fileAppender.FlushAsync();
            }
        }
    }

    static string LogFilePath(string testHash)
    {
        var traceLogsDirectory = LogFileDirectory();
        var fileName = $"{testHash}.tracelog";
        var logFilePath = Path.Combine(traceLogsDirectory.ToString(), fileName);
        return logFilePath;
    }

    public static DirectoryInfo LogFileDirectory()
    {
        // The current directory is expected to have the following structure
        // (w/ variance depending on Debug/Release and dotnet framework used (net6.0, net48 etc):
        //
        // <REPO ROOT>\source\Octopus.Tentacle.Kubernetes.Tests.Integration\bin\Debug\net6.0
        //
        // Therefore we go up 5 levels to get to the <REPO ROOT> directory,
        // from which point we can navigate to the artifacts directory.
        var currentDirectory = Directory.GetCurrentDirectory();
        var rootDirectory = new DirectoryInfo(currentDirectory).Parent!.Parent!.Parent!.Parent!.Parent!;

        var traceLogsDirectory = rootDirectory.CreateSubdirectory("artifacts").CreateSubdirectory("trace-logs");
        return traceLogsDirectory;
    }

    public async ValueTask DisposeAsync()
    {
        cancellationTokenSource.Cancel();
#pragma warning disable VSTHRD003
        await writeDataToDiskTask;
#pragma warning restore VSTHRD003
        cancellationTokenSource.Dispose();
    }
}