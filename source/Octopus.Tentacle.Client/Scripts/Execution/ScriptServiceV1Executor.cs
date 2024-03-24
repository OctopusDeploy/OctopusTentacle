using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.Observability;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;
using ILog = Octopus.Diagnostics.ILog;

namespace Octopus.Tentacle.Client.Scripts.Execution
{
    class ScriptServiceV1Executor : IScriptServiceExecutor
    {
        readonly IAsyncClientScriptService clientScriptServiceV1;
        readonly RpcCallExecutor rpcCallExecutor;
        readonly ClientOperationMetricsBuilder clientOperationMetricsBuilder;
        readonly ILog logger;

        public ScriptServiceV1Executor(
            IAsyncClientScriptService clientScriptServiceV1,
            RpcCallExecutor rpcCallExecutor,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            ILog logger)
        {
            this.clientScriptServiceV1 = clientScriptServiceV1;
            this.rpcCallExecutor = rpcCallExecutor;
            this.clientOperationMetricsBuilder = clientOperationMetricsBuilder;
            this.logger = logger;
        }
        
        public async Task<ScriptStatusResponseV3Alpha> StartScript(StartScriptCommandV3Alpha command, CancellationToken scriptExecutionCancellationToken)
        {
            var startScriptCommandV1 = MapToStartScriptCommand(command);

            var scriptTicket = await rpcCallExecutor.ExecuteWithNoRetries(
                RpcCall.Create<IScriptService>(nameof(IScriptService.StartScript)),
                async ct =>
                {
                    var result = await clientScriptServiceV1.StartScriptAsync(startScriptCommandV1, new HalibutProxyRequestOptions(ct));

                    return result;
                },
                logger,
                clientOperationMetricsBuilder,
                scriptExecutionCancellationToken).ConfigureAwait(false);

            return new ScriptStatusResponseV3Alpha(scriptTicket, ProcessState.Pending, 0, new List<ProcessOutput>(), 0);
        }

        public async Task<ScriptStatusResponseV3Alpha> GetStatus(ScriptStatusResponseV3Alpha lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            return await GetStatus(lastStatusResponse.ScriptTicket, lastStatusResponse.NextLogSequence, scriptExecutionCancellationToken);
        }

        public async Task<ScriptStatusResponseV3Alpha> GetStatus(CommandContextV3Alpha commandContext, CancellationToken scriptExecutionCancellationToken)
        {
            return await GetStatus(commandContext.ScriptTicket, commandContext.NextLogSequence, scriptExecutionCancellationToken);
        }

        async Task<ScriptStatusResponseV3Alpha> GetStatus(ScriptTicket scriptTicket, long nextLogSequence, CancellationToken scriptExecutionCancellationToken)
        {
            async Task<ScriptStatusResponse> GetStatusAction(CancellationToken ct)
            {
                var request = new ScriptStatusRequest(scriptTicket, nextLogSequence);
                var result = await clientScriptServiceV1.GetStatusAsync(request, new HalibutProxyRequestOptions(ct));

                return result;
            }

            var scriptStatusResponseV1 = await rpcCallExecutor.ExecuteWithNoRetries(
                RpcCall.Create<IScriptService>(nameof(IScriptService.GetStatus)),
                GetStatusAction,
                logger,
                clientOperationMetricsBuilder,
                scriptExecutionCancellationToken).ConfigureAwait(false);

            return MapToLatestScriptStatusResponse(scriptStatusResponseV1);
        }

        public async Task<ScriptStatusResponseV3Alpha> Cancel(ScriptStatusResponseV3Alpha lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            return await Cancel(lastStatusResponse.ScriptTicket, lastStatusResponse.NextLogSequence);
        }

        public async Task<ScriptStatusResponseV3Alpha> Cancel(CommandContextV3Alpha commandContext, CancellationToken scriptExecutionCancellationToken)
        {
            return await Cancel(commandContext.ScriptTicket, commandContext.NextLogSequence);
        }

        async Task<ScriptStatusResponseV3Alpha> Cancel(ScriptTicket scriptTicket, long nextLogSequence)
        {
            async Task<ScriptStatusResponse> CancelScriptAction(CancellationToken ct)
            {
                var request = new CancelScriptCommand(scriptTicket, nextLogSequence);
                var result = await clientScriptServiceV1.CancelScriptAsync(request, new HalibutProxyRequestOptions(ct));

                return result;
            }

            var scriptStatusResponseV1 = await rpcCallExecutor.ExecuteWithNoRetries(
                RpcCall.Create<IScriptService>(nameof(IScriptService.CancelScript)),
                CancelScriptAction,
                logger,
                clientOperationMetricsBuilder,
                // We don't want to cancel this operation as it is responsible for stopping the script executing on the Tentacle
                CancellationToken.None).ConfigureAwait(false);

            return MapToLatestScriptStatusResponse(scriptStatusResponseV1);
        }

        public async Task<ScriptStatusResponseV3Alpha> Finish(ScriptStatusResponseV3Alpha lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            return await Finish(lastStatusResponse.ScriptTicket, lastStatusResponse.NextLogSequence);
        }

        public async Task<ScriptStatusResponseV3Alpha> Finish(CommandContextV3Alpha commandContext, CancellationToken scriptExecutionCancellationToken)
        {
            return await Finish(commandContext.ScriptTicket, commandContext.NextLogSequence);
        }

        async Task<ScriptStatusResponseV3Alpha> Finish(ScriptTicket scriptTicket, long nextLogSequence)
        {
            var response = await rpcCallExecutor.ExecuteWithNoRetries(
                RpcCall.Create<IScriptService>(nameof(IScriptService.CompleteScript)),
                async ct =>
                {
                    var request = new CompleteScriptCommand(scriptTicket, nextLogSequence);
                    var result = await clientScriptServiceV1.CompleteScriptAsync(request, new HalibutProxyRequestOptions(ct));

                    return result;
                },
                logger,
                clientOperationMetricsBuilder,
                CancellationToken.None).ConfigureAwait(false);

            return MapToLatestScriptStatusResponse(response);
        }

        static StartScriptCommand MapToStartScriptCommand(StartScriptCommandV3Alpha command)
        {
            return new StartScriptCommand(
                command.ScriptBody,
                command.Isolation,
                command.ScriptIsolationMutexTimeout,
                command.IsolationMutexName!,
                command.Arguments,
                command.TaskId,
                command.Scripts,
                command.Files.ToArray());
        }

        static ScriptStatusResponseV3Alpha MapToLatestScriptStatusResponse(ScriptStatusResponse scriptStatusResponseV1)
        {
            return new ScriptStatusResponseV3Alpha(
                scriptStatusResponseV1.Ticket,
                scriptStatusResponseV1.State,
                scriptStatusResponseV1.ExitCode,
                scriptStatusResponseV1.Logs,
                scriptStatusResponseV1.NextLogSequence);
        }
    }
}