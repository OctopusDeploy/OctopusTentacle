using System;
using System.Collections.Generic;
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
    public class EventDrivenScriptExecutor : IEventDrivenScriptExecutor
    {
        readonly ITentacleClientTaskLog logger;
        readonly ITentacleClientObserver tentacleClientObserver; 
        readonly TentacleClientOptions clientOptions;
        readonly ClientsHolder clientsHolder;
        readonly RpcCallExecutor rpcCallExecutor;
        readonly TimeSpan onCancellationAbandonCompleteScriptAfter;

        public EventDrivenScriptExecutor(ITentacleClientTaskLog logger,
            ITentacleClientObserver tentacleClientOptions,
            TentacleClientOptions clientOptions,
            IHalibutRuntime halibutRuntime,
            ServiceEndPoint serviceEndPoint, TimeSpan onCancellationAbandonCompleteScriptAfter) : this(logger, tentacleClientOptions, clientOptions, halibutRuntime, serviceEndPoint, null, onCancellationAbandonCompleteScriptAfter)
        {
        }
        
        internal EventDrivenScriptExecutor(ITentacleClientTaskLog logger,
            ITentacleClientObserver tentacleClientOptions,
            TentacleClientOptions clientOptions,
            IHalibutRuntime halibutRuntime,
            ServiceEndPoint serviceEndPoint,
            ITentacleServiceDecoratorFactory? tentacleServicesDecoratorFactory, TimeSpan onCancellationAbandonCompleteScriptAfter)
        {
            this.logger = logger;
            tentacleClientObserver = tentacleClientOptions;
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
            var scriptServiceToUse = await new ScriptServicePicker(clientsHolder.CapabilitiesServiceV2, logger, rpcCallExecutor, clientOptions, operationMetricsBuilder)
                .DetermineScriptServiceVersionToUse(cancellationToken);

            var scriptOrchestratorFactory = GetNewScriptOrchestratorFactory(operationMetricsBuilder);

            var orchestrator = scriptOrchestratorFactory.CreateOrchestrator(scriptServiceToUse);
            return await orchestrator.StartScript(executeScriptCommand, startScriptIsBeingReAttempted, cancellationToken);
        }

        public async Task<(ScriptStatus, ICommandContext)> GetStatus(ICommandContext ticketForNextNextStatus, CancellationToken cancellationToken)
        {
            var operationMetricsBuilder = ClientOperationMetricsBuilder.Start();
            
            var scriptOrchestratorFactory = GetNewScriptOrchestratorFactory(operationMetricsBuilder);

            var orchestrator = scriptOrchestratorFactory.CreateOrchestrator(ticketForNextNextStatus.WhichService);

            return await orchestrator.GetStatus(ticketForNextNextStatus, cancellationToken);
        }

        public async Task<(ScriptStatus, ICommandContext)> CancelScript(ICommandContext ticketForNextNextStatus, CancellationToken cancellationToken)
        {
            var operationMetricsBuilder = ClientOperationMetricsBuilder.Start();
            
            var scriptOrchestratorFactory = GetNewScriptOrchestratorFactory(operationMetricsBuilder);

            var orchestrator = scriptOrchestratorFactory.CreateOrchestrator(ticketForNextNextStatus.WhichService);

            return await orchestrator.Cancel(ticketForNextNextStatus, cancellationToken);
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

            var orchestrator = scriptOrchestratorFactory.CreateOrchestrator(ticketForNextNextStatus.WhichService);

            return await orchestrator.Finish(ticketForNextNextStatus, cancellationToken);
        }
        
        ScriptOrchestratorFactory GetNewScriptOrchestratorFactory(ClientOperationMetricsBuilder operationMetricsBuilder)
        {
            return new ScriptOrchestratorFactory(clientsHolder, 
                new DefaultScriptObserverBackoffStrategy(), 
                rpcCallExecutor, 
                operationMetricsBuilder, 
                status => { },
                token => Task.CompletedTask,
                onCancellationAbandonCompleteScriptAfter,
                clientOptions,
                logger);
        }

    }
}