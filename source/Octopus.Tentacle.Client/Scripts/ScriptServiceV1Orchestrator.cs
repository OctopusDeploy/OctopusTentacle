using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.EventDriven;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.Logging;
using Octopus.Tentacle.Contracts.Observability;

namespace Octopus.Tentacle.Client.Scripts
{
    class ScriptServiceV1Orchestrator : IStructuredScriptOrchestrator
    {

        readonly RpcCallExecutor rpcCallExecutor;
        readonly ClientOperationMetricsBuilder clientOperationMetricsBuilder;
        readonly ITentacleClientTaskLog logger;

        readonly IAsyncClientScriptService clientScriptServiceV1;

        public ScriptServiceV1Orchestrator(
            IAsyncClientScriptService clientScriptServiceV1,
            RpcCallExecutor rpcCallExecutor,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            ITentacleClientTaskLog logger)
        {
            this.clientScriptServiceV1 = clientScriptServiceV1;
            this.rpcCallExecutor = rpcCallExecutor;
            this.clientOperationMetricsBuilder = clientOperationMetricsBuilder;
            this.logger = logger;
        }

        private StartScriptCommand Map(ExecuteScriptCommand command)
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

        private ScriptStatus MapToScriptStatus(ScriptStatusResponse scriptStatusResponse)
        {
            return new ScriptStatus(scriptStatusResponse.State, scriptStatusResponse.ExitCode, scriptStatusResponse.Logs);
        }

        private ITicketForNextStatus MapToNextStatus(ScriptStatusResponse scriptStatusResponse)
        {
            return new DefaultTicketForNextStatus(scriptStatusResponse.Ticket, scriptStatusResponse.NextLogSequence, ScriptServiceVersion.ScriptServiceVersion1);
        }

        (ScriptStatus, ITicketForNextStatus) Map(ScriptStatusResponse r)
        {
            return (MapToScriptStatus(r), MapToNextStatus(r));
        }
        
        public async Task<(ScriptStatus, ITicketForNextStatus)> StartScript(ExecuteScriptCommand command, CancellationToken scriptExecutionCancellationToken)
        {
            return Map(await _StartScript(command, scriptExecutionCancellationToken));
        }

        private async Task<ScriptStatusResponse> _StartScript(ExecuteScriptCommand executeScriptCommand, CancellationToken scriptExecutionCancellationToken)
        {
            var command = Map(executeScriptCommand);
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

        public async Task<(ScriptStatus, ITicketForNextStatus)> GetStatus(ITicketForNextStatus lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            return Map(await _GetStatus(lastStatusResponse, scriptExecutionCancellationToken));
        }

        private async Task<ScriptStatusResponse> _GetStatus(ITicketForNextStatus lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            var scriptStatusResponseV1 = await rpcCallExecutor.ExecuteWithNoRetries(
                RpcCall.Create<IScriptService>(nameof(IScriptService.GetStatus)),
                async ct =>
                {
                    var request = new ScriptStatusRequest(lastStatusResponse.ScriptTicket, lastStatusResponse.NextLogSequence);
                    var result = await clientScriptServiceV1.GetStatusAsync(request, new HalibutProxyRequestOptions(ct));

                    return result;
                },
                logger,
                clientOperationMetricsBuilder,
                scriptExecutionCancellationToken).ConfigureAwait(false);

            return scriptStatusResponseV1;
        }

        public async Task<(ScriptStatus, ITicketForNextStatus)> Cancel(ITicketForNextStatus lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            return Map(await _Cancel(lastStatusResponse, scriptExecutionCancellationToken));
        }
        
        private async Task<ScriptStatusResponse> _Cancel(ITicketForNextStatus lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            var response = await rpcCallExecutor.ExecuteWithNoRetries(
                RpcCall.Create<IScriptService>(nameof(IScriptService.CancelScript)),
                async ct =>
                {
                    var request = new CancelScriptCommand(lastStatusResponse.ScriptTicket, lastStatusResponse.NextLogSequence);
                    var result = await clientScriptServiceV1.CancelScriptAsync(request, new HalibutProxyRequestOptions(ct));

                    return result;
                },
                logger,
                clientOperationMetricsBuilder,
                CancellationToken.None).ConfigureAwait(false);

            return response;
        }

        public async Task<ScriptStatus?> Finish(ITicketForNextStatus lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            return MapToScriptStatus(await _Finish(lastStatusResponse, scriptExecutionCancellationToken));
        }

        private async Task<ScriptStatusResponse> _Finish(ITicketForNextStatus lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            var response = await rpcCallExecutor.ExecuteWithNoRetries(
                RpcCall.Create<IScriptService>(nameof(IScriptService.CompleteScript)),
                async ct =>
                {
                    var request = new CompleteScriptCommand(lastStatusResponse.ScriptTicket, lastStatusResponse.NextLogSequence);
                    var result = await clientScriptServiceV1.CompleteScriptAsync(request, new HalibutProxyRequestOptions(ct));

                    return result;
                },
                logger,
                clientOperationMetricsBuilder,
                CancellationToken.None).ConfigureAwait(false);

            return response;
        }


    }
}