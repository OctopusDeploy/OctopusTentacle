using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Halibut.Util;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Client.Services;
using Octopus.Tentacle.Client.Utils;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Observability;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using ILog = Octopus.Diagnostics.ILog;

namespace Octopus.Tentacle.Client.Scripts
{
    class ScriptServiceV1Orchestrator : ObservingScriptOrchestrator<StartScriptCommand, ScriptStatusResponse>
    {

        readonly RpcCallExecutor rpcCallExecutor;
        readonly ClientOperationMetricsBuilder clientOperationMetricsBuilder;
        readonly AsyncHalibutFeature asyncHalibutFeature;
        readonly ILog logger;

        readonly SyncAndAsyncClientScriptServiceV1 clientScriptServiceV1;

        public ScriptServiceV1Orchestrator(
            SyncAndAsyncClientScriptServiceV1 clientScriptServiceV1,
            IScriptObserverBackoffStrategy scriptObserverBackOffStrategy,
            RpcCallExecutor rpcCallExecutor,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            OnScriptStatusResponseReceived onScriptStatusResponseReceived,
            OnScriptCompleted onScriptCompleted,
            AsyncHalibutFeature asyncHalibutFeature,
            ILog logger)
            : base(scriptObserverBackOffStrategy,
                onScriptStatusResponseReceived,
                onScriptCompleted)
        {
            this.clientScriptServiceV1 = clientScriptServiceV1;
            this.rpcCallExecutor = rpcCallExecutor;
            this.clientOperationMetricsBuilder = clientOperationMetricsBuilder;
            this.asyncHalibutFeature = asyncHalibutFeature;
            this.logger = logger;
        }

        protected override StartScriptCommand Map(StartScriptCommandV2 command)
            => new(
                command.ScriptBody,
                command.Isolation,
                command.ScriptIsolationMutexTimeout,
                command.IsolationMutexName!,
                command.Arguments,
                command.TaskId,
                command.Scripts,
                command.Files.ToArray());

        protected override ScriptStatusResponseV2 Map(ScriptStatusResponse response)
            => new(
                response.Ticket,
                response.State,
                response.ExitCode,
                response.Logs,
                response.NextLogSequence);

        protected override ProcessState GetState(ScriptStatusResponse response) => response.State;

        protected override async Task<ScriptStatusResponse> StartScript(StartScriptCommand command, CancellationToken scriptExecutionCancellationToken)
        {
            var scriptTicket = await rpcCallExecutor.ExecuteWithNoRetries(
                RpcCall.Create<IScriptService>(nameof(IScriptService.StartScript)),
                async ct =>
                {
                    var result = await asyncHalibutFeature
                        .WhenDisabled(() => clientScriptServiceV1.SyncService.StartScript(command, new HalibutProxyRequestOptions(ct, CancellationToken.None)))
                        .WhenEnabled(async () => await clientScriptServiceV1.AsyncService.StartScriptAsync(command, new HalibutProxyRequestOptions(ct, CancellationToken.None)));

                    return result;
                },
                logger,
                abandonActionOnCancellation: false,
                clientOperationMetricsBuilder,
                scriptExecutionCancellationToken).ConfigureAwait(false);

            return new ScriptStatusResponse(scriptTicket,
                ProcessState.Pending,
                0,
                new List<ProcessOutput>(),
                0);
        }

        protected override async Task<ScriptStatusResponse> GetStatus(ScriptStatusResponse lastStatusResponse, CancellationToken cancellationToken)
        {
            var scriptStatusResponseV1 = await rpcCallExecutor.ExecuteWithNoRetries(
                RpcCall.Create<IScriptService>(nameof(IScriptService.GetStatus)),
                async ct =>
                {
                    var request = new ScriptStatusRequest(lastStatusResponse.Ticket, lastStatusResponse.NextLogSequence);

                    var result = await asyncHalibutFeature
                        .WhenDisabled(() => clientScriptServiceV1.SyncService.GetStatus(request, new HalibutProxyRequestOptions(ct, CancellationToken.None)))
                        .WhenEnabled(async () => await clientScriptServiceV1.AsyncService.GetStatusAsync(request, new HalibutProxyRequestOptions(ct, CancellationToken.None)));

                    return result;
                },
                logger,
                abandonActionOnCancellation: false,
                clientOperationMetricsBuilder,
                cancellationToken).ConfigureAwait(false);

            return scriptStatusResponseV1;
        }

        protected override async Task<ScriptStatusResponse> Cancel(ScriptStatusResponse lastStatusResponse, CancellationToken cancellationToken)
        {
            var response = await rpcCallExecutor.ExecuteWithNoRetries(
                RpcCall.Create<IScriptService>(nameof(IScriptService.CancelScript)),
                async ct =>
                {
                    var request = new CancelScriptCommand(lastStatusResponse.Ticket, lastStatusResponse.NextLogSequence);

                    var result = await asyncHalibutFeature
                        .WhenDisabled(() => clientScriptServiceV1.SyncService.CancelScript(request, new HalibutProxyRequestOptions(ct, CancellationToken.None)))
                        .WhenEnabled(async () => await clientScriptServiceV1.AsyncService.CancelScriptAsync(request, new HalibutProxyRequestOptions(ct, CancellationToken.None)));

                    return result;
                },
                logger,
                abandonActionOnCancellation: false,
                clientOperationMetricsBuilder,
                cancellationToken).ConfigureAwait(false);

            return response;
        }

        protected override async Task<ScriptStatusResponse> Finish(ScriptStatusResponse lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            var response = await rpcCallExecutor.ExecuteWithNoRetries(
                RpcCall.Create<IScriptService>(nameof(IScriptService.CompleteScript)),
                async ct =>
                {
                    var request = new CompleteScriptCommand(lastStatusResponse.Ticket, lastStatusResponse.NextLogSequence);

                    var result = await asyncHalibutFeature
                        .WhenDisabled(() => clientScriptServiceV1.SyncService.CompleteScript(request, new HalibutProxyRequestOptions(ct, CancellationToken.None)))
                        .WhenEnabled(async () => await clientScriptServiceV1.AsyncService.CompleteScriptAsync(request, new HalibutProxyRequestOptions(ct, CancellationToken.None)));

                    return result;
                },
                logger,
                abandonActionOnCancellation: false,
                clientOperationMetricsBuilder,
                CancellationToken.None).ConfigureAwait(false);

            OnScriptStatusResponseReceived(response);

            return response;
        }
    }
}