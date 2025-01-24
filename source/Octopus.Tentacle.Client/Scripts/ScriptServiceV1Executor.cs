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

        static ScriptOperationExecutionResult Map(ScriptStatusResponse scriptStatusResponse)
        {
            return new (
                MapToScriptStatus(scriptStatusResponse),
                new CommandContext(scriptStatusResponse.Ticket, scriptStatusResponse.NextLogSequence, ScriptServiceVersion.ScriptServiceVersion1));
        }

        static ScriptStatus MapToScriptStatus(ScriptStatusResponse scriptStatusResponse)
        {
            return new ScriptStatus(scriptStatusResponse.State, scriptStatusResponse.ExitCode, scriptStatusResponse.Logs);
        }

        public async Task<ScriptOperationExecutionResult> StartScript(ExecuteScriptCommand executeScriptCommand,
            StartScriptIsBeingReAttempted startScriptIsBeingReAttempted,
            CancellationToken scriptExecutionCancellationToken)
        {
            // Script Service v1 is not idempotent, do not allow it to be re-attempted as it may run a second time.
            if (startScriptIsBeingReAttempted == StartScriptIsBeingReAttempted.PossiblyBeingReAttempted)
            {
                throw new UnsafeStartAttemptException("Cannot start V1 script service if there is a chance it has been attempted before.");
            }

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

        public async Task<ScriptOperationExecutionResult> GetStatus(CommandContext commandContext, CancellationToken scriptExecutionCancellationToken)
        {
            var scriptStatusResponseV1 = await rpcCallExecutor.ExecuteWithNoRetries(
                RpcCall.Create<IScriptService>(nameof(IScriptService.GetStatus)),
                async ct =>
                {
                    var request = new ScriptStatusRequest(commandContext.ScriptTicket, commandContext.NextLogSequence);
                    var result = await clientScriptServiceV1.GetStatusAsync(request, new HalibutProxyRequestOptions(ct));

                    return result;
                },
                logger,
                clientOperationMetricsBuilder,
                scriptExecutionCancellationToken).ConfigureAwait(false);

            return Map(scriptStatusResponseV1);
        }

        public async Task<ScriptOperationExecutionResult> CancelScript(CommandContext commandContext)
        {
            var response = await rpcCallExecutor.ExecuteWithNoRetries(
                RpcCall.Create<IScriptService>(nameof(IScriptService.CancelScript)),
                async ct =>
                {
                    var request = new CancelScriptCommand(commandContext.ScriptTicket, commandContext.NextLogSequence);
                    var result = await clientScriptServiceV1.CancelScriptAsync(request, new HalibutProxyRequestOptions(ct));

                    return result;
                },
                logger,
                clientOperationMetricsBuilder,
                CancellationToken.None).ConfigureAwait(false);

            return Map(response);
        }

        public async Task<ScriptStatus?> CompleteScript(CommandContext lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
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

            return MapToScriptStatus(response);
        }
    }
}