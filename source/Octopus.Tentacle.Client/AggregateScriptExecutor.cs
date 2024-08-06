using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Octopus.Tentacle.Client.EventDriven;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Client.ServiceHelpers;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Logging;
using Octopus.Tentacle.Contracts.Observability;

namespace Octopus.Tentacle.Client
{
    /// <summary>
    /// Executes scripts, on the best available script service. 
    /// </summary>
    public class AggregateScriptExecutor : IScriptExecutor
    {
        readonly ITentacleClientTaskLog logger;
        readonly ITentacleClientObserver tentacleClientObserver; 
        readonly TentacleClientOptions clientOptions;
        readonly ClientsHolder clientsHolder;
        readonly RpcCallExecutor rpcCallExecutor;
        readonly TimeSpan onCancellationAbandonCompleteScriptAfter;

        public AggregateScriptExecutor(ITentacleClientTaskLog logger,
            ITentacleClientObserver tentacleClientObserver,
            TentacleClientOptions clientOptions,
            IHalibutRuntime halibutRuntime,
            ServiceEndPoint serviceEndPoint, TimeSpan onCancellationAbandonCompleteScriptAfter) : this(logger, tentacleClientObserver, clientOptions, halibutRuntime, serviceEndPoint, null, onCancellationAbandonCompleteScriptAfter)
        {
        }
        
        internal AggregateScriptExecutor(ITentacleClientTaskLog logger,
            ITentacleClientObserver tentacleClientObserver,
            TentacleClientOptions clientOptions,
            IHalibutRuntime halibutRuntime,
            ServiceEndPoint serviceEndPoint,
            ITentacleServiceDecoratorFactory? tentacleServicesDecoratorFactory, 
            TimeSpan onCancellationAbandonCompleteScriptAfter)
        {
            this.logger = logger;
            this.tentacleClientObserver = tentacleClientObserver;
            this.clientOptions = clientOptions;
            this.onCancellationAbandonCompleteScriptAfter = onCancellationAbandonCompleteScriptAfter;
            clientsHolder = new ClientsHolder(halibutRuntime, serviceEndPoint, tentacleServicesDecoratorFactory);
            rpcCallExecutor = RpcCallExecutorFactory.Create(this.clientOptions.RpcRetrySettings.RetryDuration, this.tentacleClientObserver);
        }

        public async Task<(ScriptStatus, ICommandContext)> StartScript(ExecuteScriptCommand executeScriptCommand,
            StartScriptIsBeingReAttempted startScriptIsBeingReAttempted,
            CancellationToken cancellationToken)
        {
            var operationMetricsBuilder = ClientOperationMetricsBuilder.Start();
            
            // Pick what service to use.
            var scriptServiceToUse = await DetermineScriptServiceVersionToUse(cancellationToken, operationMetricsBuilder);
            

            var scriptOrchestratorFactory = GetNewScriptOrchestratorFactory(operationMetricsBuilder);

            var orchestrator = scriptOrchestratorFactory.CreateScriptExecutor(scriptServiceToUse);
            return await orchestrator.StartScript(executeScriptCommand, startScriptIsBeingReAttempted, cancellationToken);
        }

        async Task<ScriptServiceVersion> DetermineScriptServiceVersionToUse(CancellationToken cancellationToken, ClientOperationMetricsBuilder operationMetricsBuilder)
        {
            try
            {
                return await new ScriptServicePicker(clientsHolder.CapabilitiesServiceV2, logger, rpcCallExecutor, clientOptions, operationMetricsBuilder)
                    .DetermineScriptServiceVersionToUse(cancellationToken);
            }
            catch (Exception ex) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("Script execution was cancelled", ex);
            }
        }

        public async Task<(ScriptStatus, ICommandContext)> GetStatus(ICommandContext ticketForNextNextStatus, CancellationToken cancellationToken)
        {
            var operationMetricsBuilder = ClientOperationMetricsBuilder.Start();
            
            var scriptOrchestratorFactory = GetNewScriptOrchestratorFactory(operationMetricsBuilder);

            var orchestrator = scriptOrchestratorFactory.CreateScriptExecutor(ticketForNextNextStatus.WhichService);

            return await orchestrator.GetStatus(ticketForNextNextStatus, cancellationToken);
        }

        public async Task<(ScriptStatus, ICommandContext)> CancelScript(ICommandContext ticketForNextNextStatus, CancellationToken cancellationToken)
        {
            var operationMetricsBuilder = ClientOperationMetricsBuilder.Start();
            
            var scriptOrchestratorFactory = GetNewScriptOrchestratorFactory(operationMetricsBuilder);

            var orchestrator = scriptOrchestratorFactory.CreateScriptExecutor(ticketForNextNextStatus.WhichService);

            return await orchestrator.CancelScript(ticketForNextNextStatus, cancellationToken);
        }

        public Task<ScriptStatus?> Finish(ICommandContext commandContext, CancellationToken scriptExecutionCancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<(ScriptStatus, ICommandContext)> CancelScript(ScriptTicket scriptTicket, CancellationToken cancellationToken)
        {
            var operationMetricsBuilder = ClientOperationMetricsBuilder.Start();
            throw new System.NotImplementedException();
        }

        public async Task<ScriptStatus?> CleanUpScript(ICommandContext ticketForNextNextStatus, CancellationToken cancellationToken)
        {
            var operationMetricsBuilder = ClientOperationMetricsBuilder.Start();
            
            var scriptOrchestratorFactory = GetNewScriptOrchestratorFactory(operationMetricsBuilder);

            var orchestrator = scriptOrchestratorFactory.CreateScriptExecutor(ticketForNextNextStatus.WhichService);

            return await orchestrator.CleanUpScript(ticketForNextNextStatus, cancellationToken);
        }
        
        ScriptOrchestratorFactory GetNewScriptOrchestratorFactory(ClientOperationMetricsBuilder operationMetricsBuilder)
        {
            return new ScriptOrchestratorFactory(clientsHolder, 
                rpcCallExecutor, 
                operationMetricsBuilder,
                onCancellationAbandonCompleteScriptAfter,
                clientOptions,
                logger);
        }

    }
}