using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Tar;
using k8s;
using k8s.Models;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesPodService
    {
        Task<V1PodList> ListAllPods(CancellationToken cancellationToken);
        Task WatchAllPods(string initialResourceVersion, Func<WatchEventType, V1Pod, CancellationToken, Task> onChange, Action<Exception> onError, CancellationToken cancellationToken);
        Task<V1Pod> Create(V1Pod pod, CancellationToken cancellationToken);
        Task DeleteIfExists(ScriptTicket scriptTicket, CancellationToken cancellationToken);
        Task<int> CopyFileToPodAsync(V1Pod pod, string sourceFilePath, string destinationFilePath, CancellationToken cancellationToken, string? container = null);
    }

    public class KubernetesPodService : KubernetesService, IKubernetesPodService
    {
        public KubernetesPodService(IKubernetesClientConfigProvider configProvider, ISystemLog log)
            : base(configProvider, log)
        {
        }

        public async Task<V1PodList> ListAllPods(CancellationToken cancellationToken)
        {
            return await Client.ListNamespacedPodAsync(KubernetesConfig.Namespace,
                labelSelector: OctopusLabels.ScriptTicketId,
                cancellationToken: cancellationToken);
        }

        public async Task WatchAllPods(string initialResourceVersion, Func<WatchEventType, V1Pod, CancellationToken, Task> onChange, Action<Exception> onError, CancellationToken cancellationToken)
        {
            using var response = Client.CoreV1.ListNamespacedPodWithHttpMessagesAsync(
                KubernetesConfig.Namespace,
                labelSelector: OctopusLabels.ScriptTicketId,
                resourceVersion: initialResourceVersion,
                watch: true,
                timeoutSeconds: KubernetesConfig.PodMonitorTimeoutSeconds,
                cancellationToken: cancellationToken);

            var watchErrorCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            Action<Exception> internalOnError = ex =>
            {
                //We cancel the watch explicitly (so it can be restarted)
                watchErrorCancellationTokenSource.Cancel();

                //notify there was an error
                onError(ex);
            };

            try
            {
                await foreach (var (type, pod) in response.WatchAsync<V1Pod, V1PodList>(internalOnError, cancellationToken: watchErrorCancellationTokenSource.Token))
                {
                    await onChange(type, pod, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                //Unfortunately we get an exception when the timeout hits (Server closes the connection)
                //https://github.com/kubernetes-client/csharp/issues/828
                if (ex is EndOfStreamException || ex.InnerException is EndOfStreamException)
                {
                    //Watch closed by api server, ignore this exception
                }
                else
                {
                    throw;
                }
            }
        }

        public async Task<V1Pod> Create(V1Pod pod, CancellationToken cancellationToken)
        {
            AddStandardMetadata(pod);
            return await Client.CreateNamespacedPodAsync(pod, KubernetesConfig.Namespace, cancellationToken: cancellationToken);
        }

        public async Task DeleteIfExists(ScriptTicket scriptTicket, CancellationToken cancellationToken)
            => await TryExecuteAsync(async () => await Client.DeleteNamespacedPodAsync(scriptTicket.ToKubernetesScriptPodName(), KubernetesConfig.Namespace, cancellationToken: cancellationToken));

        public async Task<int> CopyFileToPodAsync(V1Pod pod, string sourceFilePath, string destinationFilePath, CancellationToken cancellationToken, string? container = null)
        {
            // All other parameters are being validated by MuxedStreamNamespacedPodExecAsync called by NamespacedPodExecAsync
            ValidatePathParameters(sourceFilePath, destinationFilePath);

            // The callback which processes the standard input, standard output and standard error of exec method
            var handler = new ExecAsyncCallback(async (stdIn, stdOut, stdError) =>
            {
                var stdInTask = ProcessStdIn(stdIn, cancellationToken);
                var stdOutTask = ProcessStdOut(stdOut, cancellationToken);
                var stdErrorTask = ProcessStdError(stdError, cancellationToken);

                await Task.WhenAll(stdInTask, stdOutTask, stdErrorTask);
            });

            async Task ProcessStdOut(Stream stdOut, CancellationToken ct)
            {
                using var streamReader = new StreamReader(stdOut);
                while (streamReader.EndOfStream == false)
                {
                    var output = await streamReader.ReadLineAsync();
                    if (output is not null)
                    {
                        Log.Verbose($"SEND FILE OUT: {output}");
                    }
                }
            }

            async Task ProcessStdError(Stream stdError, CancellationToken ct)
            {
                using var streamReader = new StreamReader(stdError);
                while (streamReader.EndOfStream == false)
                {
                    var error = await streamReader.ReadLineAsync();
                    if (error is not null)
                    {
                        Log.Verbose($"SEND FILE ERR: {error}");
                    }
                }
            }

            async Task ProcessStdIn(Stream stdIn, CancellationToken ct)
            {
                var fileInfo = new FileInfo(destinationFilePath);
                try
                {
                    using var memoryStream = new MemoryStream();
                    await using (var inputFileStream = File.OpenRead(sourceFilePath))
                    await using (var tarOutputStream = new TarOutputStream(memoryStream, Encoding.Default))
                    {
                        tarOutputStream.IsStreamOwner = false;
                        var fileSize = inputFileStream.Length;
                        var entry = TarEntry.CreateTarEntry(fileInfo.Name);
                        entry.Size = fileSize;
                        tarOutputStream.PutNextEntry(entry);
                        await inputFileStream.CopyToAsync(tarOutputStream, ct);
                        tarOutputStream.CloseEntry();
                    }
                    memoryStream.Position = 0;
                    const int bufferSize = 31 * 1024 * 1024; // must be lower than 32 * 1024 * 1024
                    var localBuffer = new byte[bufferSize];
                    while (true)
                    {
                        var numRead = await memoryStream.ReadAsync(localBuffer, ct);
                        if (numRead <= 0)
                        {
                            break;
                        }
                        await stdIn.WriteAsync(localBuffer.AsMemory(0, numRead), ct);
                    }
                    await stdIn.FlushAsync(ct);
                }
                catch (Exception ex)
                {
                    throw new IOException($"Copy command failed: {ex.Message}");
                }
            }

            var destinationFolder = GetFolderName(destinationFilePath);

            return await Client.NamespacedPodExecAsync(
                pod.Name(),
                pod.Namespace(),
                container ?? pod.Spec.Containers.First().Name,
                new[] { "sh", "-c", $"mkdir -p {destinationFolder} && tar xmf - -C {destinationFolder}" },
                false,
                handler,
                cancellationToken);
        }

        static void ValidatePathParameters(string sourcePath, string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException($"{nameof(sourcePath)} cannot be null or whitespace");
            }

            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                throw new ArgumentException($"{nameof(destinationPath)} cannot be null or whitespace");
            }

        }

        static string GetFolderName(string filePath)
        {
            var folderName = Path.GetDirectoryName(filePath);

            return string.IsNullOrEmpty(folderName) ? "." : folderName;
        }
    }
}