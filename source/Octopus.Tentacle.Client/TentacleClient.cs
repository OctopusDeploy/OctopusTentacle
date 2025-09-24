using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.EventDriven;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Client.ServiceHelpers;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.Logging;
using Octopus.Tentacle.Contracts.Observability;
using ITentacleClientObserver = Octopus.Tentacle.Contracts.Observability.ITentacleClientObserver;

namespace Octopus.Tentacle.Client
{
    public class TentacleClient : ITentacleClient
    {
        readonly IScriptObserverBackoffStrategy scriptObserverBackOffStrategy;
        readonly ITentacleClientObserver tentacleClientObserver;
        readonly RpcCallExecutor rpcCallExecutor;
        
        readonly TentacleClientOptions clientOptions;
        readonly AllClients allClients;

        public static void CacheServiceWasNotFoundResponseMessages(IHalibutRuntime halibutRuntime)
        {
            var innerHandler = halibutRuntime.OverrideErrorResponseMessageCaching;
            halibutRuntime.OverrideErrorResponseMessageCaching = response =>
            {
                if (BackwardsCompatibleCapabilitiesV2Helper.ExceptionTypeLooksLikeTheServiceWasNotFound(response.Error!.HalibutErrorType!) ||
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
            ITentacleServiceDecoratorFactory? tentacleServicesDecoratorFactory)
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

            allClients = new AllClients(halibutRuntime, serviceEndPoint, tentacleServicesDecoratorFactory);

            rpcCallExecutor = RpcCallExecutorFactory.Create(this.clientOptions.RpcRetrySettings.RetryDuration, this.tentacleClientObserver);
        }

        public TimeSpan OnCancellationAbandonCompleteScriptAfter { get; set; } = TimeSpan.FromMinutes(1);

        RpcCallExecutor FileTransferRpcCallExecutor => 
            RpcCallExecutorFactory.Create(this.clientOptions.RpcRetrySettings.RetryDuration, this.tentacleClientObserver, clientOptions.MinimumAttemptsForInterruptedLongRunningCalls);

        public async Task<UploadResult> UploadFile(string fileName, string path, DataStream package, ITentacleClientTaskLog logger, CancellationToken cancellationToken)
        {
            var operationMetricsBuilder = ClientOperationMetricsBuilder.Start();

            async Task<UploadResult> UploadFileAction(CancellationToken ct)
            {
                logger.Info($"Beginning upload of {fileName} to Tentacle");
                var result = await allClients.ClientFileTransferServiceV1.UploadFileAsync(path, package, new HalibutProxyRequestOptions(ct));
                logger.Info("Upload complete");

                return result;
            }

            try
            {
                return await FileTransferRpcCallExecutor.Execute(
                    retriesEnabled: clientOptions.RpcRetrySettings.RetriesEnabled,
                    RpcCall.Create<IFileTransferService>(nameof(IFileTransferService.UploadFile)),
                    UploadFileAction,
                    logger,
                    operationMetricsBuilder,
                    cancellationToken).ConfigureAwait(false);
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

        public async Task<DataStream?> DownloadFile(string remotePath, ITentacleClientTaskLog logger, CancellationToken cancellationToken)
        {
            var operationMetricsBuilder = ClientOperationMetricsBuilder.Start();

            async Task<DataStream> DownloadFileAction(CancellationToken ct)
            {
                logger.Info($"Beginning download of {Path.GetFileName(remotePath)} from Tentacle");
                var result = await allClients.ClientFileTransferServiceV1.DownloadFileAsync(remotePath, new HalibutProxyRequestOptions(ct));
                logger.Info("Download complete");

                return result;
            }

            try
            {
                return await FileTransferRpcCallExecutor.Execute(
                    retriesEnabled: clientOptions.RpcRetrySettings.RetriesEnabled,
                    RpcCall.Create<IFileTransferService>(nameof(IFileTransferService.DownloadFile)),
                    DownloadFileAction,
                    logger,
                    operationMetricsBuilder,
                    cancellationToken).ConfigureAwait(false);
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

        public async Task<ScriptExecutionResult> ExecuteScript(ExecuteScriptCommand executeScriptCommand,
            OnScriptStatusResponseReceived onScriptStatusResponseReceived,
            OnScriptCompleted onScriptCompleted,
            ITentacleClientTaskLog logger,
            CancellationToken scriptExecutionCancellationToken)
        {
            var operationMetricsBuilder = ClientOperationMetricsBuilder.Start();

            try
            {
                var scriptExecutor = new ScriptExecutor(
                    allClients,
                    logger, 
                    tentacleClientObserver,
                    operationMetricsBuilder,
                    clientOptions,
                    OnCancellationAbandonCompleteScriptAfter);
                    
                var orchestrator = new ObservingScriptOrchestrator(scriptObserverBackOffStrategy,
                    onScriptStatusResponseReceived,
                    onScriptCompleted,
                    scriptExecutor);

                var result = await orchestrator.ExecuteScript(executeScriptCommand, scriptExecutionCancellationToken);

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

        public async Task<ScriptOperationExecutionResult> StartScript(
            ExecuteScriptCommand command,
            StartScriptIsBeingReAttempted startScriptIsBeingReAttempted, 
            ITentacleClientTaskLog logger, 
            CancellationToken scriptExecutionCancellationToken)
        {
            var scriptExecutor = new ScriptExecutor(
                allClients,
                logger,
                tentacleClientObserver,
                // For now, we do not support metrics for event-based operations.
                ClientOperationMetricsBuilder.Start(),
                clientOptions,
                OnCancellationAbandonCompleteScriptAfter);

            return await scriptExecutor.StartScript(command, startScriptIsBeingReAttempted, scriptExecutionCancellationToken);
        }

        public async Task<ScriptOperationExecutionResult> GetStatus(CommandContext commandContext, ITentacleClientTaskLog logger, CancellationToken scriptExecutionCancellationToken)
        {
            var scriptExecutor = new ScriptExecutor(
            allClients,
                logger,
                tentacleClientObserver,
                // For now, we do not support metrics for event-based operations.
                ClientOperationMetricsBuilder.Start(),
                clientOptions,
                OnCancellationAbandonCompleteScriptAfter);

            return await scriptExecutor.GetStatus(commandContext, scriptExecutionCancellationToken);
        }

        public async Task<ScriptOperationExecutionResult> CancelScript(CommandContext commandContext, ITentacleClientTaskLog logger)
        {
            var scriptExecutor = new ScriptExecutor(
                allClients,
                logger,
                tentacleClientObserver,
                // For now, we do not support metrics for event-based operations.
                ClientOperationMetricsBuilder.Start(),
                clientOptions,
                OnCancellationAbandonCompleteScriptAfter);

            return await scriptExecutor.CancelScript(commandContext);
        }

        public async Task<ScriptStatus?> CompleteScript(CommandContext commandContext, ITentacleClientTaskLog logger, CancellationToken scriptExecutionCancellationToken)
        {
            var scriptExecutor = new ScriptExecutor(
                allClients,
                logger,
                tentacleClientObserver,
                // For now, we do not support metrics for event-based operations.
                ClientOperationMetricsBuilder.Start(),
                clientOptions,
                OnCancellationAbandonCompleteScriptAfter);

            return await scriptExecutor.CompleteScript(commandContext, scriptExecutionCancellationToken);
        }

        public void Dispose()
        {
        }
    }
}