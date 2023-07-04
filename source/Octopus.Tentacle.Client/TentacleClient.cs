﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Client.Decorators;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Observability;
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
        readonly ITentacleClientObserver tentacleClientObserver;
        readonly RpcCallExecutor rpcCallExecutor;
        readonly IClientScriptService scriptServiceV1;
        readonly IClientScriptServiceV2 scriptServiceV2;
        readonly IClientFileTransferService fileTransferServiceV1;
        readonly IClientCapabilitiesServiceV2 capabilitiesServiceV2;

        public TentacleClient(
            ServiceEndPoint serviceEndPoint,
            IHalibutRuntime halibutRuntime,
            IScriptObserverBackoffStrategy scriptObserverBackOffStrategy,
            TimeSpan retryDuration,
            ITentacleClientObserver tentacleClientObserver) : 
                this(serviceEndPoint, halibutRuntime, scriptObserverBackOffStrategy, retryDuration, tentacleClientObserver, null)
        {
        }

        internal TentacleClient(
            ServiceEndPoint serviceEndPoint,
            IHalibutRuntime halibutRuntime,
            IScriptObserverBackoffStrategy scriptObserverBackOffStrategy,
            TimeSpan retryDuration,
            ITentacleClientObserver tentacleClientObserver,
            ITentacleServiceDecorator? tentacleServicesDecorator)
        {
            this.scriptObserverBackOffStrategy = scriptObserverBackOffStrategy;
            this.tentacleClientObserver = tentacleClientObserver;

            var innerHandler = halibutRuntime.OverrideErrorResponseMessageCaching;
            halibutRuntime.OverrideErrorResponseMessageCaching = response =>
            {
                if (BackwardsCompatibleCapabilitiesV2Helper.ExceptionTypeLooksLikeTheServiceWasNotFound(response.Error.HalibutErrorType) ||
                    BackwardsCompatibleCapabilitiesV2Helper.ExceptionMessageLooksLikeTheServiceWasNotFound(response.Error.Message))
                {
                    return true;
                }

                // TentacleClient doesn't own the HalibutRuntime so allow other handlers to be configured that override the error caching behaviour
                return innerHandler?.Invoke(response) ?? false;
            };

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
            
            rpcCallExecutor = RpcCallExecutorFactory.Create(retryDuration, tentacleClientObserver);
        }

        public TimeSpan OnCancellationAbandonCompleteScriptAfter { get; set; } = TimeSpan.FromMinutes(1);

        public async Task<UploadResult> UploadFile(string fileName, string path, DataStream package, ILog logger, CancellationToken cancellationToken)
        {
            var operationMetricsBuilder = ClientOperationMetricsBuilder.Start();

            try
            {
                return await rpcCallExecutor.ExecuteWithRetries(
                    RpcCall.Create<IFileTransferService>(nameof(IFileTransferService.UploadFile)),
                    ct =>
                    {
                        logger.Info($"Beginning upload of {fileName} to Tentacle");
                        var result = fileTransferServiceV1.UploadFile(path, package, new HalibutProxyRequestOptions(ct));
                        logger.Info("Upload complete");
                        return result;
                    },
                    logger,
                    abandonActionOnCancellation: false,
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
                tentacleClientObserver.UploadFileCompleted(operationMetrics);
            }
        }

        public async Task<DataStream?> DownloadFile(string remotePath, ILog logger, CancellationToken cancellationToken)
        {
            var operationMetricsBuilder = ClientOperationMetricsBuilder.Start();

            try
            {
                var dataStream = await rpcCallExecutor.ExecuteWithRetries(
                    RpcCall.Create<IFileTransferService>(nameof(IFileTransferService.DownloadFile)),
                    ct =>
                    {
                        logger.Info($"Beginning download of {Path.GetFileName(remotePath)} from Tentacle");
                        var result = fileTransferServiceV1.DownloadFile(remotePath, new HalibutProxyRequestOptions(ct));
                        logger.Info("Download complete");
                        return result;
                    },
                    logger,
                    abandonActionOnCancellation: false,
                    operationMetricsBuilder,
                    cancellationToken).ConfigureAwait(false);

                return (DataStream?)dataStream;
            }
            catch (Exception e)
            {
                operationMetricsBuilder.Failure(e, cancellationToken);
                throw;
            }
            finally
            {
                var operationMetrics = operationMetricsBuilder.Build();
                tentacleClientObserver.DownloadFileCompleted(operationMetrics);
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
                tentacleClientObserver.ExecuteScriptCompleted(operationMetrics);
            }
        }

        public void Dispose()
        {
        }
    }
}