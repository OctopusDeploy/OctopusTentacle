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
        readonly ClientOperationMetricsBuilder operationMetricsBuilder; 
        readonly TentacleClientOptions clientOptions;
        readonly AllClients allClients;
        readonly RpcCallExecutor rpcCallExecutor;
        readonly TimeSpan onCancellationAbandonCompleteScriptAfter;
        
        public ScriptExecutor(AllClients allClients,
            ITentacleClientTaskLog logger,
            ITentacleClientObserver tentacleClientObserver, 
            TentacleClientOptions clientOptions,
            TimeSpan onCancellationAbandonCompleteScriptAfter)
        : this(
            allClients,
            logger,
            tentacleClientObserver,
            // For now, we do not support operation based metrics when used outside the TentacleClient. So just plug in builder to discard.
            ClientOperationMetricsBuilder.Start(),
            clientOptions,
            onCancellationAbandonCompleteScriptAfter)
        {
        }

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

        public async Task<ScriptExecutorResult> StartScript(ExecuteScriptCommand executeScriptCommand,
            StartScriptIsBeingReAttempted startScriptIsBeingReAttempted,
            CancellationToken cancellationToken)
        {
            var scriptServiceToUse = await DetermineScriptServiceVersionToUse(cancellationToken);

            var scriptExecutorFactory = CreateScriptExecutorFactory();

            var scriptExecutor = scriptExecutorFactory.CreateScriptExecutor(scriptServiceToUse);
            return await scriptExecutor.StartScript(executeScriptCommand, startScriptIsBeingReAttempted, cancellationToken);
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

        public async Task<ScriptExecutorResult> GetStatus(CommandContext ticketForNextNextStatus, CancellationToken cancellationToken)
        {
            var scriptExecutorFactory = CreateScriptExecutorFactory();

            var scriptExecutor = scriptExecutorFactory.CreateScriptExecutor(ticketForNextNextStatus.ScripServiceVersionUsed);

            return await scriptExecutor.GetStatus(ticketForNextNextStatus, cancellationToken);
        }

        public async Task<ScriptExecutorResult> CancelScript(CommandContext ticketForNextNextStatus, CancellationToken cancellationToken)
        {
            var scriptExecutorFactory = CreateScriptExecutorFactory();

            var scriptExecutor = scriptExecutorFactory.CreateScriptExecutor(ticketForNextNextStatus.ScripServiceVersionUsed);

            return await scriptExecutor.CancelScript(ticketForNextNextStatus, cancellationToken);
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

    }
}