using System;
using System.Threading;
using System.Threading.Tasks;
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
    public class ScriptExecutor : IScriptExecutor
    {
        readonly ITentacleClientTaskLog logger;
        readonly ITentacleClientObserver tentacleClientObserver; 
        readonly TentacleClientOptions clientOptions;
        readonly AllClients allClients;
        readonly RpcCallExecutor rpcCallExecutor;
        readonly TimeSpan onCancellationAbandonCompleteScriptAfter;
        
        public ScriptExecutor(AllClients allClients,
            ITentacleClientTaskLog logger,
            ITentacleClientObserver tentacleClientObserver,
            TentacleClientOptions clientOptions,
            TimeSpan onCancellationAbandonCompleteScriptAfter)
        {
            this.allClients = allClients;
            this.logger = logger;
            this.tentacleClientObserver = tentacleClientObserver;
            this.clientOptions = clientOptions;
            this.onCancellationAbandonCompleteScriptAfter = onCancellationAbandonCompleteScriptAfter;
            rpcCallExecutor = RpcCallExecutorFactory.Create(this.clientOptions.RpcRetrySettings.RetryDuration, this.tentacleClientObserver);
        }

        public async Task<ScriptExecutorResult> StartScript(ExecuteScriptCommand executeScriptCommand,
            StartScriptIsBeingReAttempted startScriptIsBeingReAttempted,
            CancellationToken cancellationToken)
        {
            var operationMetricsBuilder = ClientOperationMetricsBuilder.Start();
            
            var scriptServiceToUse = await DetermineScriptServiceVersionToUse(cancellationToken, operationMetricsBuilder);

            var scriptExecutorFactory = CreateScriptExecutorFactory(operationMetricsBuilder);

            var scriptExecutor = scriptExecutorFactory.CreateScriptExecutor(scriptServiceToUse);
            return await scriptExecutor.StartScript(executeScriptCommand, startScriptIsBeingReAttempted, cancellationToken);
        }

        async Task<ScriptServiceVersion> DetermineScriptServiceVersionToUse(CancellationToken cancellationToken, ClientOperationMetricsBuilder operationMetricsBuilder)
        {
            try
            {
                var scriptServiceVersionSelector = new ScriptServiceVersionSelector(allClients.CapabilitiesServiceV2, logger, rpcCallExecutor, clientOptions, operationMetricsBuilder);
                return await scriptServiceVersionSelector.DetermineScriptServiceVersionToUse(cancellationToken);
            }
            catch (Exception ex) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("Script execution was cancelled", ex);
            }
        }

        public async Task<ScriptExecutorResult> GetStatus(CommandContext ticketForNextNextStatus, CancellationToken cancellationToken)
        {
            var operationMetricsBuilder = ClientOperationMetricsBuilder.Start();
            
            var scriptExecutorFactory = CreateScriptExecutorFactory(operationMetricsBuilder);

            var scriptExecutor = scriptExecutorFactory.CreateScriptExecutor(ticketForNextNextStatus.ScripServiceVersionUsed);

            return await scriptExecutor.GetStatus(ticketForNextNextStatus, cancellationToken);
        }

        public async Task<ScriptExecutorResult> CancelScript(CommandContext ticketForNextNextStatus, CancellationToken cancellationToken)
        {
            var operationMetricsBuilder = ClientOperationMetricsBuilder.Start();
            
            var scriptExecutorFactory = CreateScriptExecutorFactory(operationMetricsBuilder);

            var scriptExecutor = scriptExecutorFactory.CreateScriptExecutor(ticketForNextNextStatus.ScripServiceVersionUsed);

            return await scriptExecutor.CancelScript(ticketForNextNextStatus, cancellationToken);
        }
        
        public async Task<ScriptStatus?> CompleteScript(CommandContext ticketForNextNextStatus, CancellationToken cancellationToken)
        {
            var operationMetricsBuilder = ClientOperationMetricsBuilder.Start();
            
            var scriptExecutorFactory = CreateScriptExecutorFactory(operationMetricsBuilder);

            var scriptExecutor = scriptExecutorFactory.CreateScriptExecutor(ticketForNextNextStatus.ScripServiceVersionUsed);

            return await scriptExecutor.CompleteScript(ticketForNextNextStatus, cancellationToken);
        }
        
        ScriptExecutorFactory CreateScriptExecutorFactory(ClientOperationMetricsBuilder operationMetricsBuilder)
        {
            return new ScriptExecutorFactory(allClients, 
                rpcCallExecutor, 
                operationMetricsBuilder,
                onCancellationAbandonCompleteScriptAfter,
                clientOptions,
                logger);
        }

    }
}