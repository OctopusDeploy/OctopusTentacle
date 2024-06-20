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
            rpcCallExecutor = RpcCallExecutorFactory.Create(this.clientOptions.RpcRetrySettings.RetryDuration, this.tentacleClientObserver)
        }

        public async Task<(ScriptStatus, ITicketForNextStatus)> StartScript(ExecuteScriptCommand executeScriptCommand,
            HasStartScriptBeenCalledBefore hasStartScriptBeenCalledBefore,
            CancellationToken cancellationToken)
        {
            var operationMetricsBuilder = ClientOperationMetricsBuilder.Start();
            
            // Pick what service to use.
            var scriptServiceToUse = await new ScriptServicePicker(clientsHolder.CapabilitiesServiceV2, logger, rpcCallExecutor, clientOptions, operationMetricsBuilder)
                .DetermineScriptServiceVersionToUse(cancellationToken);
            
            // And start the script
            if (scriptServiceToUse == ScriptServiceVersion.ScriptServiceVersion1 && hasStartScriptBeenCalledBefore == HasStartScriptBeenCalledBefore.ItMayHaveBeen)
            {
                return (new ScriptStatus(ProcessState.Complete, ScriptExitCodes.UnknownScriptExitCode, new List<ProcessOutput>()), new DefaultTicketForNextStatus(new ScriptTicket(Guid.NewGuid().ToString()), 0, ScriptServiceVersion.ScriptServiceVersion1));
            }

            var scriptOrchestratorFactory = new ScriptOrchestratorFactory(clientsHolder, 
                new DefaultScriptObserverBackoffStrategy(), 
                rpcCallExecutor, 
                operationMetricsBuilder, 
                status => { },
                token => Task.CompletedTask,
                onCancellationAbandonCompleteScriptAfter,
                clientOptions,
                logger);

            var orchestrator = scriptOrchestratorFactory.CreateOrchestrator(scriptServiceToUse);
            
            orchestrator.s
            
            // and return stuff.
            throw new System.NotImplementedException();
        }

        public Task<(ScriptStatus, ITicketForNextStatus)> GetStatus(ITicketForNextStatus ticketForNextNextStatus, CancellationToken cancellationToken)
        {

            ScriptOrchestratorFactory scriptOrchestratorFactory = null!;
            var orch = scriptOrchestratorFactory.CreateOrchestrator(ticketForNextNextStatus.WhichService);
            
            throw new System.NotImplementedException();
        }

        public Task<(ScriptStatus, ITicketForNextStatus)> CancelScript(ITicketForNextStatus ticketForNextNextStatus, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<(ScriptStatus, ITicketForNextStatus)> CancelScript(ScriptTicket scriptTicket, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task CleanUpScript(ScriptTicket scriptTicket, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }
    }
}