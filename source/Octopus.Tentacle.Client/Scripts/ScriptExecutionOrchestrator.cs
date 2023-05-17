using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Octopus.Tentacle.Client.Capabilities;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using ILog = Octopus.Diagnostics.ILog;

namespace Octopus.Tentacle.Client.Scripts
{
    internal class ScriptExecutionOrchestrator : IDisposable
    {
        readonly IScriptObserverBackoffStrategy scriptObserverBackOffStrategy;
        readonly CancellationTokenSource connectCancellationTokenSource;
        readonly RpcCallRetryHandler rpcCallRetryHandler;
        readonly StartScriptCommandV2 startScriptCommand;
        readonly Action<ScriptStatusResponseV2> onScriptStatusResponseReceived;
        readonly Func<CancellationToken, Task> onScriptCompleted;
        readonly ILog logger;
        readonly IScriptService scriptServiceV1;
        readonly IScriptServiceV2 scriptServiceV2;
        readonly ICapabilitiesServiceV2 capabilitiesServiceV2;

        public ScriptExecutionOrchestrator(
            IScriptService scriptServiceV1,
            IScriptServiceV2 scriptServiceV2,
            ICapabilitiesServiceV2 capabilitiesServiceV2,
            CancellationTokenSource connectCancellationTokenSource,
            IScriptObserverBackoffStrategy scriptObserverBackOffStrategy,
            RpcCallRetryHandler rpcCallRetryHandler,
            StartScriptCommandV2 startScriptCommand,
            Action<ScriptStatusResponseV2> onScriptStatusResponseReceived,
            Func<CancellationToken, Task> onScriptCompleted,
            ILog logger)
        {
            this.scriptServiceV1 = scriptServiceV1;
            this.scriptServiceV2 = scriptServiceV2;
            this.capabilitiesServiceV2 = capabilitiesServiceV2;
            this.connectCancellationTokenSource = connectCancellationTokenSource;
            this.scriptObserverBackOffStrategy = scriptObserverBackOffStrategy;
            this.rpcCallRetryHandler = rpcCallRetryHandler;
            this.startScriptCommand = startScriptCommand;
            this.onScriptStatusResponseReceived = onScriptStatusResponseReceived;
            this.onScriptCompleted = onScriptCompleted;
            this.logger = logger;
        }

        public async Task<ScriptStatusResponseV2> ExecuteScript(CancellationToken cancellationToken)
        {
            ScriptStatusResponseV2 scriptStatusResponse;
            ScriptServiceVersion scriptServiceVersionToUse;

            // Only use this cancellation token for connection. Once connected, we use the cancellation token within taskContext
            using (cancellationToken.Register(connectCancellationTokenSource.Cancel))
            {
                scriptServiceVersionToUse = await DetermineScriptServiceVersionToUse(logger, cancellationToken);

                if (scriptServiceVersionToUse == ScriptServiceVersion.Version2)
                {
                    scriptStatusResponse = await rpcCallRetryHandler.ExecuteWithRetries(
                        ct =>
                        {
                            using (ct.Register(connectCancellationTokenSource.Cancel))
                            {
                                return scriptServiceV2.StartScript(startScriptCommand);
                            }
                        },
                        logger,
                        cancellationToken);
                }
                else
                {
                    var startScriptCommandV1 = Map(startScriptCommand);
                    var scriptTicket = scriptServiceV1.StartScript(startScriptCommandV1);

                    scriptStatusResponse = Map(scriptTicket);
                }
            }

            return await ObserveUntilCompleteThenFinish(scriptServiceVersionToUse, scriptStatusResponse, cancellationToken);
        }

        public void Dispose()
        {
            connectCancellationTokenSource?.Dispose();
        }

        async Task<ScriptServiceVersion> DetermineScriptServiceVersionToUse(ILog logger, CancellationToken cancellationToken)
        {
            logger.Verbose("Determining ScriptService version to use");

            var tentacleCapabilities = await rpcCallRetryHandler.ExecuteWithRetries(
                ct =>
                {
                    using (ct.Register(connectCancellationTokenSource.Cancel))
                    {
                        return capabilitiesServiceV2.GetCapabilities();
                    }
                },
                logger,
                cancellationToken);

            logger.Verbose($"Discovered Tentacle capabilities: {string.Join(",", tentacleCapabilities.SupportedCapabilities)}");

            if (tentacleCapabilities.HasScriptServiceV2())
            {
                logger.Verbose("Using ScriptServiceV2");
                logger.Verbose($"RPC call retries are enabled. Retry timeout {rpcCallRetryHandler.RetryTimeout.TotalSeconds} seconds");
                return ScriptServiceVersion.Version2;
            }

            logger.Verbose("RPC call retries are enabled but will not be used as a compatible ScriptService was not found. Please upgrade Tentacle to enable this feature.");

            logger.Verbose("Using ScriptServiceV1");
            return ScriptServiceVersion.Version1;
        }

        async Task<ScriptStatusResponseV2> ObserveUntilCompleteThenFinish(
            ScriptServiceVersion scriptServiceVersionToUse,
            ScriptStatusResponseV2 scriptStatusResponse,
            CancellationToken cancellationToken)
        {
            onScriptStatusResponseReceived(scriptStatusResponse);

            var lastScriptStatus = await ObserveUntilComplete(scriptServiceVersionToUse, scriptStatusResponse, cancellationToken);

            await onScriptCompleted(cancellationToken);

            lastScriptStatus = Finish(scriptServiceVersionToUse, lastScriptStatus, cancellationToken);

            return lastScriptStatus;
        }

        async Task<ScriptStatusResponseV2> ObserveUntilComplete(
            ScriptServiceVersion scriptServiceVersionToUse,
            ScriptStatusResponseV2 scriptStatusResponse,
            CancellationToken cancellationToken)
        {
            var lastStatus = scriptStatusResponse;
            var iteration = 0;

            while (lastStatus.State != ProcessState.Complete)
            {
                lastStatus = await GetStatusOrCancel(scriptServiceVersionToUse, lastStatus, cancellationToken);
                onScriptStatusResponseReceived(lastStatus);

                if (lastStatus.State == ProcessState.Complete)
                {
                    continue;
                }

                try
                {
                    await Task.Delay(scriptObserverBackOffStrategy.GetBackoff(++iteration), cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // Proceed when the current task has been cancelled
                }
            }

            return lastStatus;
        }

        async Task<ScriptStatusResponseV2> GetStatusOrCancel(
            ScriptServiceVersion scriptServiceVersionToUse,
            ScriptStatusResponseV2 lastStatusResponse,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return await Cancel(scriptServiceVersionToUse, lastStatusResponse, cancellationToken);
            }

            return await GetStatus(scriptServiceVersionToUse, lastStatusResponse, cancellationToken);
        }

        async Task<ScriptStatusResponseV2> GetStatus(
            ScriptServiceVersion scriptServiceVersionToUse,
            ScriptStatusResponseV2 lastStatusResponse,
            CancellationToken cancellationToken)
        {
            if (scriptServiceVersionToUse == ScriptServiceVersion.Version2)
            {
                return await rpcCallRetryHandler.ExecuteWithRetries(
                    ct => scriptServiceV2.GetStatus(new ScriptStatusRequestV2(lastStatusResponse.Ticket, lastStatusResponse.NextLogSequence)),
                    logger,
                    cancellationToken);
            }

            var scriptStatusResponseV1 = scriptServiceV1.GetStatus(new ScriptStatusRequest(lastStatusResponse.Ticket, lastStatusResponse.NextLogSequence));
            return Map(scriptStatusResponseV1);
        }

        async Task<ScriptStatusResponseV2> Cancel(
            ScriptServiceVersion scriptServiceVersionToUse,
            ScriptStatusResponseV2 lastStatusResponse,
            CancellationToken _)
        {
            if (scriptServiceVersionToUse == ScriptServiceVersion.Version2)
            {
                return await rpcCallRetryHandler.ExecuteWithRetries(
                    ct => scriptServiceV2.CancelScript(new CancelScriptCommandV2(lastStatusResponse.Ticket, lastStatusResponse.NextLogSequence)),
                    logger,
                    // We don't want to cancel this operation as it is responsible for stopping the script executing on the Tentacle
                    CancellationToken.None);
            }

            var scriptStatusResponseV1 = scriptServiceV1.CancelScript(new CancelScriptCommand(lastStatusResponse.Ticket, lastStatusResponse.NextLogSequence));
            return Map(scriptStatusResponseV1);
        }

        ScriptStatusResponseV2 Finish(
            ScriptServiceVersion scriptServiceVersionToUse,
            ScriptStatusResponseV2 lastStatusResponse,
            CancellationToken _)
        {
            ScriptStatusResponseV2 completeStatus;

            if (scriptServiceVersionToUse == ScriptServiceVersion.Version2)
            {
                // Best effort cleanup of Tentacle
                try
                {
                    scriptServiceV2.CompleteScript(new CompleteScriptCommandV2(lastStatusResponse.Ticket));
                }
                catch (HalibutClientException ex)
                {
                    logger.Warn("Failed to cleanup the working directory on Tentacle");
                    logger.Verbose(ex);
                }

                completeStatus = lastStatusResponse;
            }
            else
            {
                var completeStatusV1 = scriptServiceV1.CompleteScript(new CompleteScriptCommand(lastStatusResponse.Ticket, lastStatusResponse.NextLogSequence));
                completeStatus = Map(completeStatusV1);
                onScriptStatusResponseReceived(completeStatus);
            }

            return completeStatus;
        }

        static ScriptStatusResponseV2 Map(ScriptTicket ticket)
        {
            return new ScriptStatusResponseV2(ticket,
                ProcessState.Pending,
                0,
                new List<ProcessOutput>(),
                0);
        }

        static ScriptStatusResponseV2 Map(ScriptStatusResponse scriptStatusResponse)
        {
            return new ScriptStatusResponseV2(scriptStatusResponse.Ticket,
                scriptStatusResponse.State,
                scriptStatusResponse.ExitCode,
                scriptStatusResponse.Logs,
                scriptStatusResponse.NextLogSequence);
        }

        static StartScriptCommand Map(StartScriptCommandV2 command)
        {
            return new StartScriptCommand(
                command.ScriptBody,
                command.Isolation,
                command.ScriptIsolationMutexTimeout,
                command.IsolationMutexName!,
                command.Arguments,
                command.TaskId!,
                command.Scripts,
                command.Files?.ToArray() ?? Array.Empty<ScriptFile>());
        }
    }
}
