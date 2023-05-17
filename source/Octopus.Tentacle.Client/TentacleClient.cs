using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Octopus.Tentacle.Client.Decorators;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Polly.Timeout;
using ILog = Octopus.Diagnostics.ILog;

namespace Octopus.Tentacle.Client
{
    public class TentacleClient : ITentacleClient, IDisposable
    {
        readonly IScriptObserverBackoffStrategy scriptObserverBackOffStrategy;
        readonly CancellationTokenSource connectCancellationTokenSource;
        readonly RpcCallRetryHandler rpcCallRetryHandler;
        readonly IScriptService scriptServiceV1;
        readonly IScriptServiceV2 scriptServiceV2;
        readonly IScriptServiceV2 scriptServiceV2WithoutConnectCancellation;
        readonly IFileTransferService fileTransferServiceV1;
        readonly ICapabilitiesServiceV2 capabilitiesServiceV2;

        public TentacleClient(ServiceEndPoint serviceEndPoint,
            IHalibutRuntime halibutRuntime,
            IScriptObserverBackoffStrategy scriptObserverBackOffStrategy,
            ITentacleServiceDecorator? tentacleServicesDecorator,
            TimeSpan retryDuration)
        {
            this.scriptObserverBackOffStrategy = scriptObserverBackOffStrategy;

            connectCancellationTokenSource = new CancellationTokenSource();

            scriptServiceV1 = halibutRuntime.CreateClient<IScriptService>(serviceEndPoint, connectCancellationTokenSource.Token);
            scriptServiceV2 = halibutRuntime.CreateClient<IScriptServiceV2>(serviceEndPoint, connectCancellationTokenSource.Token);
            scriptServiceV2WithoutConnectCancellation = halibutRuntime.CreateClient<IScriptServiceV2>(serviceEndPoint, CancellationToken.None);
            fileTransferServiceV1 = halibutRuntime.CreateClient<IFileTransferService>(serviceEndPoint, connectCancellationTokenSource.Token);
            capabilitiesServiceV2 = halibutRuntime.CreateClient<ICapabilitiesServiceV2>(serviceEndPoint, connectCancellationTokenSource.Token).WithBackwardsCompatability();

            var exceptionDecorator = new HalibutExceptionTentacleServiceDecorator();
            scriptServiceV2 = exceptionDecorator.Decorate(scriptServiceV2);
            scriptServiceV2WithoutConnectCancellation = exceptionDecorator.Decorate(scriptServiceV2WithoutConnectCancellation);
            capabilitiesServiceV2 = exceptionDecorator.Decorate(capabilitiesServiceV2);

            if (tentacleServicesDecorator != null)
            {
                scriptServiceV1 = tentacleServicesDecorator.Decorate(scriptServiceV1);
                scriptServiceV2 = tentacleServicesDecorator.Decorate(scriptServiceV2);
                scriptServiceV2WithoutConnectCancellation = tentacleServicesDecorator.Decorate(scriptServiceV2WithoutConnectCancellation);
                fileTransferServiceV1 = tentacleServicesDecorator.Decorate(fileTransferServiceV1);
                capabilitiesServiceV2 = tentacleServicesDecorator.Decorate(capabilitiesServiceV2);
            }

            rpcCallRetryHandler = new RpcCallRetryHandler(retryDuration, TimeoutStrategy.Pessimistic);
        }

        public TimeSpan OnCancellationAbandonCompleteScriptAfter { get; set; } = TimeSpan.FromMinutes(1);

        public void Dispose()
        {
            connectCancellationTokenSource?.Dispose();
        }

        public async Task<UploadResult> UploadFile(string fileName, string path, DataStream package, ILog logger, CancellationToken cancellationToken)
        {
            return await rpcCallRetryHandler.ExecuteWithRetries(
                ct =>
                {
                    using (ct.Register(connectCancellationTokenSource.TryCancel))
                    {
                        logger.Info($"Beginning upload of {fileName} to Tentacle");
                        var result = fileTransferServiceV1.UploadFile(path, package);
                        logger.Info("Upload complete");
                        return result;
                    }
                },
                logger,
                cancellationToken,
                abandonActionOnCancellation: false).ConfigureAwait(false);
        }

        public async Task<DataStream?> DownloadFile(string remotePath, ILog logger, CancellationToken cancellationToken)
        {
            var dataStream = await rpcCallRetryHandler.ExecuteWithRetries(
                ct =>
                {
                    using (ct.Register(connectCancellationTokenSource.TryCancel))
                    {
                        logger.Info($"Beginning download of {Path.GetFileName(remotePath)} from Tentacle");
                        var result = fileTransferServiceV1.DownloadFile(remotePath);
                        logger.Info("Download complete");
                        return result;
                    }
                },
                logger,
                cancellationToken,
                abandonActionOnCancellation: false).ConfigureAwait(false);

            return (DataStream?)dataStream;
        }

        public async Task<ScriptExecutionResult> ExecuteScript(
            StartScriptCommandV2 startScriptCommand,
            Action<ScriptStatusResponseV2> onScriptStatusResponseReceived,
            Func<CancellationToken, Task> onScriptCompleted,
            ILog logger,
            CancellationToken cancellationToken)
        {
            using var orchestrator = new ScriptExecutionOrchestrator(
                scriptServiceV1,
                scriptServiceV2,
                scriptServiceV2WithoutConnectCancellation,
                capabilitiesServiceV2,
                connectCancellationTokenSource,
                scriptObserverBackOffStrategy,
                rpcCallRetryHandler,
                startScriptCommand,
                onScriptStatusResponseReceived,
                onScriptCompleted,
                OnCancellationAbandonCompleteScriptAfter,
                logger);

            var result = await orchestrator.ExecuteScript(cancellationToken).ConfigureAwait(false);
            return new ScriptExecutionResult(result.State, result.ExitCode);
        }
    }
}