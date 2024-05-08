using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.Logging;
using Octopus.Tentacle.Contracts.Observability;

namespace Octopus.Tentacle.Client.Scripts
{
    class ScriptServiceV1Orchestrator : ObservingScriptOrchestrator<StartScriptCommand, ScriptStatusResponse>
    {

        readonly RpcCallExecutor rpcCallExecutor;
        readonly ClientOperationMetricsBuilder clientOperationMetricsBuilder;
        readonly ITaskLog logger;

        readonly IAsyncClientScriptService clientScriptServiceV1;

        public ScriptServiceV1Orchestrator(
            IAsyncClientScriptService clientScriptServiceV1,
            IScriptObserverBackoffStrategy scriptObserverBackOffStrategy,
            RpcCallExecutor rpcCallExecutor,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            OnScriptStatusResponseReceived onScriptStatusResponseReceived,
            OnScriptCompleted onScriptCompleted,
            TentacleClientOptions clientOptions,
            ITaskLog logger)
            : base(scriptObserverBackOffStrategy,
                onScriptStatusResponseReceived,
                onScriptCompleted,
                clientOptions)
        {
            this.clientScriptServiceV1 = clientScriptServiceV1;
            this.rpcCallExecutor = rpcCallExecutor;
            this.clientOperationMetricsBuilder = clientOperationMetricsBuilder;
            this.logger = logger;
        }

        protected override StartScriptCommand Map(ExecuteScriptCommand command)
        {
            if (command is not ExecuteShellScriptCommand shellScriptCommand)
                throw new InvalidOperationException($"{nameof(ScriptServiceV2Orchestrator)} only supports commands of type {nameof(ExecuteShellScriptCommand)}.");

            return new StartScriptCommand(
                shellScriptCommand.ScriptBody,
                shellScriptCommand.IsolationConfiguration.IsolationLevel,
                shellScriptCommand.IsolationConfiguration.MutexTimeout,
                shellScriptCommand.IsolationConfiguration.MutexName,
                shellScriptCommand.Arguments,
                shellScriptCommand.TaskId,
                shellScriptCommand.Scripts,
                shellScriptCommand.Files.ToArray());
        }

        protected override ScriptExecutionStatus MapToStatus(ScriptStatusResponse response)
            => new(response.Logs);

        protected override ScriptExecutionResult MapToResult(ScriptStatusResponse response)
            => new(response.State, response.ExitCode);

        protected override ProcessState GetState(ScriptStatusResponse response) => response.State;

        protected override async Task<ScriptStatusResponse> StartScript(StartScriptCommand command, CancellationToken scriptExecutionCancellationToken)
        {
            var scriptTicket = await rpcCallExecutor.ExecuteWithNoRetries(
                RpcCall.Create<IScriptService>(nameof(IScriptService.StartScript)),
                async ct =>
                {
                    var result = await clientScriptServiceV1.StartScriptAsync(command, new HalibutProxyRequestOptions(ct));

                    return result;
                },
                logger,
                clientOperationMetricsBuilder,
                scriptExecutionCancellationToken).ConfigureAwait(false);

            return new ScriptStatusResponse(scriptTicket, ProcessState.Pending, 0, new List<ProcessOutput>(), 0);
        }

        protected override async Task<ScriptStatusResponse> GetStatus(ScriptStatusResponse lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            var scriptStatusResponseV1 = await rpcCallExecutor.ExecuteWithNoRetries(
                RpcCall.Create<IScriptService>(nameof(IScriptService.GetStatus)),
                async ct =>
                {
                    var request = new ScriptStatusRequest(lastStatusResponse.Ticket, lastStatusResponse.NextLogSequence);
                    var result = await clientScriptServiceV1.GetStatusAsync(request, new HalibutProxyRequestOptions(ct));

                    return result;
                },
                logger,
                clientOperationMetricsBuilder,
                scriptExecutionCancellationToken).ConfigureAwait(false);

            return scriptStatusResponseV1;
        }

        protected override async Task<ScriptStatusResponse> Cancel(ScriptStatusResponse lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            var response = await rpcCallExecutor.ExecuteWithNoRetries(
                RpcCall.Create<IScriptService>(nameof(IScriptService.CancelScript)),
                async ct =>
                {
                    var request = new CancelScriptCommand(lastStatusResponse.Ticket, lastStatusResponse.NextLogSequence);
                    var result = await clientScriptServiceV1.CancelScriptAsync(request, new HalibutProxyRequestOptions(ct));

                    return result;
                },
                logger,
                clientOperationMetricsBuilder,
                CancellationToken.None).ConfigureAwait(false);

            return response;
        }

        protected override async Task<ScriptStatusResponse> Finish(ScriptStatusResponse lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            var response = await rpcCallExecutor.ExecuteWithNoRetries(
                RpcCall.Create<IScriptService>(nameof(IScriptService.CompleteScript)),
                async ct =>
                {
                    var request = new CompleteScriptCommand(lastStatusResponse.Ticket, lastStatusResponse.NextLogSequence);
                    var result = await clientScriptServiceV1.CompleteScriptAsync(request, new HalibutProxyRequestOptions(ct));

                    return result;
                },
                logger,
                clientOperationMetricsBuilder,
                CancellationToken.None).ConfigureAwait(false);

            OnScriptStatusResponseReceived(response);

            return response;
        }
    }
}