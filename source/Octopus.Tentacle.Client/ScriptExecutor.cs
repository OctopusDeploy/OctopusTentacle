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
    class ScriptExecutor : IScriptExecutor
    {
        readonly ITentacleClientTaskLog logger;
        readonly ClientOperationMetricsBuilder operationMetricsBuilder; 
        readonly TentacleClientOptions clientOptions;
        readonly AllClients allClients;
        readonly RpcCallExecutor rpcCallExecutor;
        readonly TimeSpan onCancellationAbandonCompleteScriptAfter;
        
        internal ScriptExecutor(AllClients allClients,
            ITentacleClientTaskLog logger,
            ITentacleClientObserver tentacleClientObserver,
            ClientOperationMetricsBuilder operationMetricsBuilder,
            TentacleClientOptions clientOptions,
            TimeSpan onCancellationAbandonCompleteScriptAfter)
        {
            this.allClients = allClients;
            this.logger = logger;
            this.clientOptions = clientOptions;
            this.onCancellationAbandonCompleteScriptAfter = onCancellationAbandonCompleteScriptAfter;
            this.operationMetricsBuilder = operationMetricsBuilder;
            rpcCallExecutor = RpcCallExecutorFactory.Create(this.clientOptions.RpcRetrySettings.RetryDuration, tentacleClientObserver);
        }

        public async Task<ScriptOperationExecutionResult> StartScript(ExecuteScriptCommand executeScriptCommand,
            StartScriptIsBeingReAttempted startScriptIsBeingReAttempted,
            CancellationToken cancellationToken)
        {
            var scriptServiceVersionToUse = await DetermineScriptServiceVersionToUse(cancellationToken);

            var scriptExecutorFactory = CreateScriptExecutorFactory();
            var scriptExecutor = scriptExecutorFactory.CreateScriptExecutor(scriptServiceVersionToUse);

            return await scriptExecutor.StartScript(executeScriptCommand, startScriptIsBeingReAttempted, cancellationToken);
        }

        public async Task<ScriptOperationExecutionResult> GetStatus(CommandContext ticketForNextNextStatus, CancellationToken cancellationToken)
        {
            var scriptExecutorFactory = CreateScriptExecutorFactory();
            var scriptExecutor = scriptExecutorFactory.CreateScriptExecutor(ticketForNextNextStatus.ScripServiceVersionUsed);

            return await scriptExecutor.GetStatus(ticketForNextNextStatus, cancellationToken);
        }

        public async Task<ScriptOperationExecutionResult> CancelScript(CommandContext ticketForNextNextStatus)
        {
            var scriptExecutorFactory = CreateScriptExecutorFactory();
            var scriptExecutor = scriptExecutorFactory.CreateScriptExecutor(ticketForNextNextStatus.ScripServiceVersionUsed);

            return await scriptExecutor.CancelScript(ticketForNextNextStatus);
        }
        
        public async Task<ScriptStatus?> CompleteScript(CommandContext ticketForNextNextStatus, CancellationToken cancellationToken)
        {
            var scriptExecutorFactory = CreateScriptExecutorFactory();
            var scriptExecutor = scriptExecutorFactory.CreateScriptExecutor(ticketForNextNextStatus.ScripServiceVersionUsed);

            return await scriptExecutor.CompleteScript(ticketForNextNextStatus, cancellationToken);
        }
        
        ScriptExecutorFactory CreateScriptExecutorFactory()
        {
            return new ScriptExecutorFactory(allClients, 
                rpcCallExecutor, 
                operationMetricsBuilder,
                onCancellationAbandonCompleteScriptAfter,
                clientOptions,
                logger);
        }

        async Task<ScriptServiceVersion> DetermineScriptServiceVersionToUse(CancellationToken cancellationToken)
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
    }
}