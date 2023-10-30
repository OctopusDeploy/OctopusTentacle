using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.Decorators;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Client.Scripts;
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

        readonly IAsyncClientScriptService scriptServiceV1;
        readonly IAsyncClientScriptServiceV2 scriptServiceV2;
        readonly IAsyncClientFileTransferService clientFileTransferServiceV1;
        readonly IAsyncClientCapabilitiesServiceV2 capabilitiesServiceV2;
        readonly TentacleClientOptions clientOptions;

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
            TentacleClientOptions clientOptions
        ) : this(serviceEndPoint, halibutRuntime, scriptObserverBackOffStrategy, tentacleClientObserver, clientOptions, null)
        {
        }

        internal TentacleClient(
            ServiceEndPoint serviceEndPoint,
            IHalibutRuntime halibutRuntime,
            IScriptObserverBackoffStrategy scriptObserverBackOffStrategy,
            ITentacleClientObserver tentacleClientObserver,
            TentacleClientOptions clientOptions,
            ITentacleServiceDecoratorFactory? tentacleServicesDecorator)
        {
            this.scriptObserverBackOffStrategy = scriptObserverBackOffStrategy;
            this.tentacleClientObserver = tentacleClientObserver.DecorateWithNonThrowingTentacleClientObserver();

            this.clientOptions = clientOptions;

            if (halibutRuntime.OverrideErrorResponseMessageCaching == null)
            {
                // Best effort to make sure the HalibutRuntime has been configured to Cache ServiceNotFoundExceptions
                // Do not configure the HalibutRuntime here as it should only be done once and configuring it here will result in it being performed a lot
                throw new ArgumentException("Ensure that TentacleClient.CacheServiceWasNotFoundResponseMessages has been called for the HalibutRuntime", nameof(halibutRuntime));
            }

            scriptServiceV1 = halibutRuntime.CreateAsyncClient<IScriptService, IAsyncClientScriptService>(serviceEndPoint);
            scriptServiceV2 = halibutRuntime.CreateAsyncClient<IScriptServiceV2, IAsyncClientScriptServiceV2>(serviceEndPoint);
            clientFileTransferServiceV1 = halibutRuntime.CreateAsyncClient<IFileTransferService, IAsyncClientFileTransferService>(serviceEndPoint);
            capabilitiesServiceV2 = halibutRuntime.CreateAsyncClient<ICapabilitiesServiceV2, IAsyncClientCapabilitiesServiceV2>(serviceEndPoint).WithBackwardsCompatability();

            var exceptionDecorator = new HalibutExceptionTentacleServiceDecoratorFactory();
            scriptServiceV2 = exceptionDecorator.Decorate(scriptServiceV2);
            capabilitiesServiceV2 = exceptionDecorator.Decorate(capabilitiesServiceV2);

            if (tentacleServicesDecorator != null)
            {
                scriptServiceV1 = tentacleServicesDecorator.Decorate(scriptServiceV1);
                scriptServiceV2 = tentacleServicesDecorator.Decorate(scriptServiceV2);
                clientFileTransferServiceV1 = tentacleServicesDecorator.Decorate(clientFileTransferServiceV1);
                capabilitiesServiceV2 = tentacleServicesDecorator.Decorate(capabilitiesServiceV2);
            }
            
            rpcCallExecutor = RpcCallExecutorFactory.Create(this.clientOptions.RpcRetrySettings.RetryDuration, this.tentacleClientObserver);
        }

        public TimeSpan OnCancellationAbandonCompleteScriptAfter { get; set; } = TimeSpan.FromMinutes(1);

        public async Task<UploadResult> UploadFile(string fileName, string path, DataStream package, ILog logger, CancellationToken cancellationToken)
        {
            var operationMetricsBuilder = ClientOperationMetricsBuilder.Start();

            async Task<UploadResult> UploadFileAction(CancellationToken ct)
            {
                await Task.CompletedTask.ConfigureAwait(false);

                logger.Info($"Beginning upload of {fileName} to Tentacle");

                var result = await clientFileTransferServiceV1.UploadFileAsync(path, package, new HalibutProxyRequestOptions(ct, CancellationToken.None));

                logger.Info("Upload complete");
                return result;
            }

            try
            {
                if (clientOptions.RpcRetrySettings.RetriesEnabled)
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

                var result = await clientFileTransferServiceV1.DownloadFileAsync(remotePath, new HalibutProxyRequestOptions(ct, CancellationToken.None));

                logger.Info("Download complete");
                return result;
            }

            try
            {
                if (clientOptions.RpcRetrySettings.RetriesEnabled)
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
            OnScriptStatusResponseReceived onScriptStatusResponseReceived,
            OnScriptCompleted onScriptCompleted,
            ILog logger,
            CancellationToken scriptExecutionCancellationToken)
        {
            var operationMetricsBuilder = ClientOperationMetricsBuilder.Start();

            try
            {
                var factory = new ScriptOrchestratorFactory(
                    scriptServiceV1,
                    scriptServiceV2,
                    capabilitiesServiceV2,
                    scriptObserverBackOffStrategy,
                    rpcCallExecutor,
                    operationMetricsBuilder,
                    onScriptStatusResponseReceived,
                    onScriptCompleted,
                    OnCancellationAbandonCompleteScriptAfter,
                    clientOptions,
                    logger);

                var orchestrator = await factory.CreateOrchestrator(scriptExecutionCancellationToken);

                var result = await orchestrator.ExecuteScript(startScriptCommand, scriptExecutionCancellationToken);

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