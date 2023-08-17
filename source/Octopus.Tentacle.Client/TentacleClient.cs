using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.ServiceModel;
using Halibut.Util;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Client.Decorators;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Client.Services;
using Octopus.Tentacle.Client.Utils;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.Observability;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using ILog = Octopus.Diagnostics.ILog;
using ITentacleClientObserver = Octopus.Tentacle.Contracts.Observability.ITentacleClientObserver;

namespace Octopus.Tentacle.Client
{
    public class TentacleClient : ITentacleClient
    {
        readonly IScriptObserverBackoffStrategy scriptObserverBackOffStrategy;
        readonly ITentacleClientObserver tentacleClientObserver;
        readonly RpcCallExecutor rpcCallExecutor;
        readonly RpcRetrySettings rpcRetrySettings;
        readonly AsyncHalibutFeature asyncHalibutFeature;

        readonly SyncAndAsyncClientScriptServiceV1 scriptServiceV1;
        readonly SyncAndAsyncClientScriptServiceV2 scriptServiceV2;
        readonly SyncAndAsyncClientFileTransferServiceV1 clientFileTransferServiceV1;
        readonly SyncAndAsyncClientCapabilitiesServiceV2 capabilitiesServiceV2;

        public static void CacheServiceWasNotFoundResponseMessages(IHalibutRuntime halibutRuntime)
        {
            var innerHandler = halibutRuntime.OverrideErrorResponseMessageCaching;
            halibutRuntime.OverrideErrorResponseMessageCaching = response =>
            {
                if (BackwardsCompatibleCapabilitiesV2Helper.ExceptionTypeLooksLikeTheServiceWasNotFound(response.Error.HalibutErrorType) ||
                    BackwardsCompatibleCapabilitiesV2Helper.ExceptionMessageLooksLikeTheServiceWasNotFound(response.Error.Message))
                {
                    return true;
                }

                return innerHandler?.Invoke(response) ?? false;
            };
        }

        public TentacleClient(
            ServiceEndPoint serviceEndPoint,
            IHalibutRuntime halibutRuntime,
            IScriptObserverBackoffStrategy scriptObserverBackOffStrategy,
            ITentacleClientObserver tentacleClientObserver,
            RpcRetrySettings rpcRetrySettings
        ) : this(serviceEndPoint, halibutRuntime, scriptObserverBackOffStrategy, tentacleClientObserver, rpcRetrySettings, null)
        {
        }
        
        internal TentacleClient(
            ServiceEndPoint serviceEndPoint,
            IHalibutRuntime halibutRuntime,
            IScriptObserverBackoffStrategy scriptObserverBackOffStrategy,
            ITentacleClientObserver tentacleClientObserver,
            RpcRetrySettings rpcRetrySettings,
            ITentacleServiceDecorator? tentacleServicesDecorator)
        {
            this.scriptObserverBackOffStrategy = scriptObserverBackOffStrategy;
            this.tentacleClientObserver = tentacleClientObserver.DecorateWithNonThrowingTentacleClientObserver();
            this.rpcRetrySettings = rpcRetrySettings;
            this.asyncHalibutFeature = halibutRuntime.AsyncHalibutFeature;

            if (halibutRuntime.OverrideErrorResponseMessageCaching == null)
            {
                // Best effort to make sure the HalibutRuntime has been configured to Cache ServiceNotFoundExceptions
                // Do not configure the HalibutRuntime here as it should only be done once and configuring it here will result in it being performed a lot
                throw new ArgumentException("Ensure that TentacleClient.CacheServiceWasNotFoundResponseMessages has been called for the HalibutRuntime", nameof(halibutRuntime));
            }

            if (asyncHalibutFeature == AsyncHalibutFeature.Disabled)
            {
                var syncScriptServiceV1 = halibutRuntime.CreateClient<IScriptService, IClientScriptService>(serviceEndPoint);
                var syncScriptServiceV2 = halibutRuntime.CreateClient<IScriptServiceV2, IClientScriptServiceV2>(serviceEndPoint);
                var syncFileTransferServiceV1 = halibutRuntime.CreateClient<IFileTransferService, IClientFileTransferService>(serviceEndPoint);
                var syncCapabilitiesServiceV2 = halibutRuntime.CreateClient<ICapabilitiesServiceV2, IClientCapabilitiesServiceV2>(serviceEndPoint).WithBackwardsCompatability();

                var exceptionDecorator = new HalibutExceptionTentacleServiceDecorator();
                syncScriptServiceV2 = exceptionDecorator.Decorate(syncScriptServiceV2);
                syncCapabilitiesServiceV2 = exceptionDecorator.Decorate(syncCapabilitiesServiceV2);

                if (tentacleServicesDecorator != null)
                {
                    syncScriptServiceV1 = tentacleServicesDecorator.Decorate(syncScriptServiceV1);
                    syncScriptServiceV2 = tentacleServicesDecorator.Decorate(syncScriptServiceV2);
                    syncFileTransferServiceV1 = tentacleServicesDecorator.Decorate(syncFileTransferServiceV1);
                    syncCapabilitiesServiceV2 = tentacleServicesDecorator.Decorate(syncCapabilitiesServiceV2);
                }

                scriptServiceV1 = new(syncScriptServiceV1, null);
                scriptServiceV2 = new(syncScriptServiceV2, null);
                clientFileTransferServiceV1 = new(syncFileTransferServiceV1, null);
                capabilitiesServiceV2 = new(syncCapabilitiesServiceV2, null);
            }
            else
            {
                var asyncScriptServiceV1 = halibutRuntime.CreateAsyncClient<IScriptService, IAsyncClientScriptService>(serviceEndPoint);
                var asyncScriptServiceV2 = halibutRuntime.CreateAsyncClient<IScriptServiceV2, IAsyncClientScriptServiceV2>(serviceEndPoint);
                var asyncFileTransferServiceV1 = halibutRuntime.CreateAsyncClient<IFileTransferService, IAsyncClientFileTransferService>(serviceEndPoint);
                var asyncCapabilitiesServiceV2 = halibutRuntime.CreateAsyncClient<ICapabilitiesServiceV2, IAsyncClientCapabilitiesServiceV2>(serviceEndPoint).WithBackwardsCompatability();
                
                var exceptionDecorator = new HalibutExceptionTentacleServiceDecorator();
                asyncScriptServiceV2 = exceptionDecorator.Decorate(asyncScriptServiceV2);
                asyncCapabilitiesServiceV2 = exceptionDecorator.Decorate(asyncCapabilitiesServiceV2);

                if (tentacleServicesDecorator != null)
                {
                    asyncScriptServiceV1 = tentacleServicesDecorator.Decorate(asyncScriptServiceV1);
                    asyncScriptServiceV2 = tentacleServicesDecorator.Decorate(asyncScriptServiceV2);
                    asyncFileTransferServiceV1 = tentacleServicesDecorator.Decorate(asyncFileTransferServiceV1);
                    asyncCapabilitiesServiceV2 = tentacleServicesDecorator.Decorate(asyncCapabilitiesServiceV2);
                }

                scriptServiceV1 = new(null, asyncScriptServiceV1);
                scriptServiceV2 = new(null, asyncScriptServiceV2);
                clientFileTransferServiceV1 = new(null, asyncFileTransferServiceV1);
                capabilitiesServiceV2 = new(null, asyncCapabilitiesServiceV2);
            }

            rpcCallExecutor = RpcCallExecutorFactory.Create(rpcRetrySettings.RetryDuration, this.tentacleClientObserver);
        }

        public TimeSpan OnCancellationAbandonCompleteScriptAfter { get; set; } = TimeSpan.FromMinutes(1);

        public async Task<UploadResult> UploadFile(string fileName, string path, DataStream package, ILog logger, CancellationToken cancellationToken)
        {
            var operationMetricsBuilder = ClientOperationMetricsBuilder.Start();

            async Task<UploadResult> UploadFileAction(CancellationToken ct)
            {
                await Task.CompletedTask.ConfigureAwait(false);

                logger.Info($"Beginning upload of {fileName} to Tentacle");

                var result = await asyncHalibutFeature
                    .WhenDisabled(() => clientFileTransferServiceV1.SyncService.UploadFile(path, package, new HalibutProxyRequestOptions(ct, CancellationToken.None)))
                    .WhenEnabled(async () => await clientFileTransferServiceV1.AsyncService.UploadFileAsync(path, package, new HalibutProxyRequestOptions(ct, CancellationToken.None)));

                logger.Info("Upload complete");
                return result;
            }

            try
            {
                if (rpcRetrySettings.RetriesEnabled)
                {
                    return await rpcCallExecutor.ExecuteWithRetries(
                        RpcCall.Create<IFileTransferService>(nameof(IFileTransferService.UploadFile)),
                        UploadFileAction,
                        logger,
                        abandonActionOnCancellation: false,
                        operationMetricsBuilder,
                        cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    return await rpcCallExecutor.ExecuteWithNoRetries(
                        RpcCall.Create<IFileTransferService>(nameof(IFileTransferService.UploadFile)),
                        UploadFileAction,
                        logger,
                        abandonActionOnCancellation: false,
                        operationMetricsBuilder,
                        cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                operationMetricsBuilder.Failure(e, cancellationToken);
                throw;
            }
            finally
            {
                var operationMetrics = operationMetricsBuilder.Build();
                tentacleClientObserver.UploadFileCompleted(operationMetrics, logger);
            }
        }

        public async Task<DataStream?> DownloadFile(string remotePath, ILog logger, CancellationToken cancellationToken)
        {
            var operationMetricsBuilder = ClientOperationMetricsBuilder.Start();

            async Task<DataStream> DownloadFileAction(CancellationToken ct)
            {
                await Task.CompletedTask.ConfigureAwait(false);

                logger.Info($"Beginning download of {Path.GetFileName(remotePath)} from Tentacle");

                var result = await asyncHalibutFeature
                    .WhenDisabled(() => clientFileTransferServiceV1.SyncService.DownloadFile(remotePath, new HalibutProxyRequestOptions(ct, CancellationToken.None)))
                    .WhenEnabled(async () => await clientFileTransferServiceV1.AsyncService.DownloadFileAsync(remotePath, new HalibutProxyRequestOptions(ct, CancellationToken.None)));
                
                logger.Info("Download complete");
                return result;
            }

            try
            {
                if (rpcRetrySettings.RetriesEnabled)
                {
                    return await rpcCallExecutor.ExecuteWithRetries(
                        RpcCall.Create<IFileTransferService>(nameof(IFileTransferService.DownloadFile)),
                        DownloadFileAction,
                        logger,
                        abandonActionOnCancellation: false,
                        operationMetricsBuilder,
                        cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    return await rpcCallExecutor.ExecuteWithNoRetries(
                        RpcCall.Create<IFileTransferService>(nameof(IFileTransferService.DownloadFile)),
                        DownloadFileAction,
                        logger,
                        abandonActionOnCancellation: false,
                        operationMetricsBuilder,
                        cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                operationMetricsBuilder.Failure(e, cancellationToken);
                throw;
            }
            finally
            {
                var operationMetrics = operationMetricsBuilder.Build();
                tentacleClientObserver.DownloadFileCompleted(operationMetrics, logger);
            }
        }

        public async Task<ScriptExecutionResult> ExecuteScript(
            StartScriptCommandV2 startScriptCommand,
            Action<ScriptStatusResponseV2> onScriptStatusResponseReceived,
            Func<CancellationToken, Task> onScriptCompleted,
            ILog logger,
            CancellationToken scriptExecutionCancellationToken)
        {
            var operationMetricsBuilder = ClientOperationMetricsBuilder.Start();

            try
            {
                using var orchestrator = new ScriptExecutionOrchestrator(
                    scriptServiceV1,
                    scriptServiceV2,
                    capabilitiesServiceV2,
                    scriptObserverBackOffStrategy,
                    rpcCallExecutor,
                    operationMetricsBuilder,
                    startScriptCommand,
                    onScriptStatusResponseReceived,
                    onScriptCompleted,
                    OnCancellationAbandonCompleteScriptAfter,
                    rpcRetrySettings,
                    asyncHalibutFeature,
                    logger);

                var result = await orchestrator.ExecuteScript(scriptExecutionCancellationToken).ConfigureAwait(false);
                return new ScriptExecutionResult(result.State, result.ExitCode);
            }
            catch (Exception e)
            {
                operationMetricsBuilder.Failure(e, scriptExecutionCancellationToken);
                throw;
            }
            finally
            {
                var operationMetrics = operationMetricsBuilder.Build();
                tentacleClientObserver.ExecuteScriptCompleted(operationMetrics, logger);
            }
        }

        public void Dispose()
        {
        }
    }
}
