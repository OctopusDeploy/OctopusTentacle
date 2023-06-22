using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Client.Decorators;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.Observability;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using ILog = Octopus.Diagnostics.ILog;

namespace Octopus.Tentacle.Client
{
    public class TentacleClient : ITentacleClient
    {
        readonly IScriptObserverBackoffStrategy scriptObserverBackOffStrategy;
        readonly IRpcCallRetryHandler rpcCallRetryHandler;
        readonly IClientScriptService scriptServiceV1;
        readonly IClientScriptServiceV2 scriptServiceV2;
        readonly IClientFileTransferService fileTransferServiceV1;
        readonly IClientCapabilitiesServiceV2 capabilitiesServiceV2;

        public TentacleClient(
            ServiceEndPoint serviceEndPoint,
            IHalibutRuntime halibutRuntime,
            IScriptObserverBackoffStrategy scriptObserverBackOffStrategy,
            TimeSpan retryDuration,
            ITentacleServiceDecorator? tentacleServicesDecorator,
            IRpcCallObserver? rpcCallObserver)
        {
            this.scriptObserverBackOffStrategy = scriptObserverBackOffStrategy;

            scriptServiceV1 = halibutRuntime.CreateClient<IScriptService, IClientScriptService>(serviceEndPoint);
            scriptServiceV2 = halibutRuntime.CreateClient<IScriptServiceV2, IClientScriptServiceV2>(serviceEndPoint);
            fileTransferServiceV1 = halibutRuntime.CreateClient<IFileTransferService, IClientFileTransferService>(serviceEndPoint);
            capabilitiesServiceV2 = halibutRuntime.CreateClient<ICapabilitiesServiceV2, IClientCapabilitiesServiceV2>(serviceEndPoint).WithBackwardsCompatability();

            var exceptionDecorator = new HalibutExceptionTentacleServiceDecorator();
            scriptServiceV2 = exceptionDecorator.Decorate(scriptServiceV2);
            capabilitiesServiceV2 = exceptionDecorator.Decorate(capabilitiesServiceV2);

            if (tentacleServicesDecorator != null)
            {
                scriptServiceV1 = tentacleServicesDecorator.Decorate(scriptServiceV1);
                scriptServiceV2 = tentacleServicesDecorator.Decorate(scriptServiceV2);
                fileTransferServiceV1 = tentacleServicesDecorator.Decorate(fileTransferServiceV1);
                capabilitiesServiceV2 = tentacleServicesDecorator.Decorate(capabilitiesServiceV2);
            }

            rpcCallRetryHandler = RpcCallRetryHandlerFactory.Create(retryDuration, rpcCallObserver);
        }

        public TimeSpan OnCancellationAbandonCompleteScriptAfter { get; set; } = TimeSpan.FromMinutes(1);

        public async Task<UploadResult> UploadFile(string fileName, string path, DataStream package, ILog logger, CancellationToken cancellationToken)
        {
            return await rpcCallRetryHandler.ExecuteWithRetries(
                nameof(fileTransferServiceV1.UploadFile),
                ct =>
                {
                    logger.Info($"Beginning upload of {fileName} to Tentacle");
                    var result = fileTransferServiceV1.UploadFile(path, package, new HalibutProxyRequestOptions(ct));
                    logger.Info("Upload complete");
                    return result;
                },
                logger,
                cancellationToken,
                abandonActionOnCancellation: false).ConfigureAwait(false);
        }

        public async Task<DataStream?> DownloadFile(string remotePath, ILog logger, CancellationToken cancellationToken)
        {
            var dataStream = await rpcCallRetryHandler.ExecuteWithRetries(
                nameof(fileTransferServiceV1.DownloadFile),
                ct =>
                {
                    logger.Info($"Beginning download of {Path.GetFileName(remotePath)} from Tentacle");
                    var result = fileTransferServiceV1.DownloadFile(remotePath, new HalibutProxyRequestOptions(ct));
                    logger.Info("Download complete");
                    return result;
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
            CancellationToken scriptExecutionCancellationToken)
        {
            using var orchestrator = new ScriptExecutionOrchestrator(
                scriptServiceV1,
                scriptServiceV2,
                capabilitiesServiceV2,
                scriptObserverBackOffStrategy,
                rpcCallRetryHandler,
                startScriptCommand,
                onScriptStatusResponseReceived,
                onScriptCompleted,
                OnCancellationAbandonCompleteScriptAfter,
                logger);

            var result = await orchestrator.ExecuteScript(scriptExecutionCancellationToken).ConfigureAwait(false);
            return new ScriptExecutionResult(result.State, result.ExitCode);
        }

        public void Dispose()
        {
        }
    }
}