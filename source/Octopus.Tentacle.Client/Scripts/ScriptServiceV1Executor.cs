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
    class ScriptServiceV1Executor : IScriptExecutor
    {
        readonly RpcCallExecutor rpcCallExecutor;
        readonly ClientOperationMetricsBuilder clientOperationMetricsBuilder;
        readonly ITentacleClientTaskLog logger;

        readonly IAsyncClientScriptService clientScriptServiceV1;

        public ScriptServiceV1Executor(
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

        StartScriptCommand Map(ExecuteScriptCommand command)
        {
            if (command is not ExecuteShellScriptCommand shellScriptCommand)
                throw new InvalidOperationException($"{nameof(ScriptServiceV2Executor)} only supports commands of type {nameof(ExecuteShellScriptCommand)}.");

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

        ScriptStatus MapToScriptStatus(ScriptStatusResponse scriptStatusResponse)
        {
            return new ScriptStatus(scriptStatusResponse.State, scriptStatusResponse.ExitCode, scriptStatusResponse.Logs);
        }

        ICommandContext MapToNextStatus(ScriptStatusResponse scriptStatusResponse)
        {
            return new DefaultCommandContext(scriptStatusResponse.Ticket, scriptStatusResponse.NextLogSequence, ScriptServiceVersion.ScriptServiceVersion1);
        }

        (ScriptStatus, ICommandContext) Map(ScriptStatusResponse r)
        {
            return (MapToScriptStatus(r), MapToNextStatus(r));
        }

        public async Task<(ScriptStatus, ICommandContext)> StartScript(ExecuteScriptCommand executeScriptCommand,
            StartScriptIsBeingReAttempted startScriptIsBeingReAttempted,
            CancellationToken scriptExecutionCancellationToken)
        {
            // Script Service v1 is not idempotent, do not allow it to be re-attempted as it may run a second time.
            if (startScriptIsBeingReAttempted == StartScriptIsBeingReAttempted.PossiblyBeingReAttempted)
                return (new ScriptStatus(ProcessState.Complete,
                        ScriptExitCodes.UnknownScriptExitCode,
                        new List<ProcessOutput>()),
                    // TODO: We should try to encourage a DefaultCommandContext which will do nothing perhaps set the ScriptServiceVersion to
                    // one that always returns the above exit code.
                    new DefaultCommandContext(new ScriptTicket(Guid.NewGuid().ToString()), 0, ScriptServiceVersion.ScriptServiceVersion1));
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

            return Map(new ScriptStatusResponse(scriptTicket, ProcessState.Pending, 0, new List<ProcessOutput>(), 0));
        }

        public async Task<(ScriptStatus, ICommandContext)> GetStatus(ICommandContext commandContext, CancellationToken scriptExecutionCancellationToken)
        {
            return Map(await _GetStatus(commandContext, scriptExecutionCancellationToken));
        }

        async Task<ScriptStatusResponse> _GetStatus(ICommandContext lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
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

        public async Task<(ScriptStatus, ICommandContext)> CancelScript(ICommandContext lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            return Map(await _Cancel(lastStatusResponse, scriptExecutionCancellationToken));
        }

        async Task<ScriptStatusResponse> _Cancel(ICommandContext lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
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

        public async Task<ScriptStatus?> CleanUpScript(ICommandContext lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            return MapToScriptStatus(await _CleanUpScript(lastStatusResponse, scriptExecutionCancellationToken));
        }

        async Task<ScriptStatusResponse> _CleanUpScript(ICommandContext lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
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