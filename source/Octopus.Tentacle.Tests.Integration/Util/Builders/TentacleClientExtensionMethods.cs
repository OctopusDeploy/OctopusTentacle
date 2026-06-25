using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.EventDriven;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Logging;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.ExtensionMethods;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders
{
    public static class TentacleClientExtensionMethods
    {
        public static async Task<(ScriptExecutionResult ScriptExecutionResult, List<ProcessOutput> ProcessOutput)> ExecuteScript(
            this TentacleClient tentacleClient,
            ExecuteScriptCommand executeScriptCommand,
            CancellationToken token,
            OnScriptStatusResponseReceived? onScriptStatusResponseReceivedAction = null,
            ITentacleClientTaskLog? log = null)
        {
            var logs = new List<ProcessOutput>();
            var finalResponse = await tentacleClient.ExecuteScript(executeScriptCommand,
                onScriptStatusResponseReceived =>
                {
                    if (onScriptStatusResponseReceivedAction != null)
                    {
                        onScriptStatusResponseReceivedAction(onScriptStatusResponseReceived);
                    }

                    logs.AddRange(onScriptStatusResponseReceived.Logs);
                },
                cts => Task.CompletedTask,
                new SerilogLoggerBuilder().Build().ForContext<TentacleClient>().ToITentacleTaskLog().Chain(log),
                token).ConfigureAwait(false);
            return (finalResponse, logs);
        }

        public static async Task<UploadResult> UploadFile(
            this TentacleClient tentacleClient,
            string remotePath,
            DataStream upload,
            CancellationToken token,
            ITentacleClientTaskLog? log = null)
        {
            var result = await tentacleClient.UploadFile(Path.GetFileName(remotePath),
                remotePath,
                upload,
                new SerilogLoggerBuilder().Build().ForContext<TentacleClient>().ToITentacleTaskLog().Chain(log),
                token).ConfigureAwait(false);

            return result;
        }

        public static async Task<ScriptStatusResponseV2> AbandonScript(
            this TentacleClient tentacleClient,
            ScriptTicket scriptTicket,
            CancellationToken token,
            ITentacleClientTaskLog? log = null)
        {
            return await tentacleClient.AbandonScript(scriptTicket,
                new SerilogLoggerBuilder().Build().ForContext<TentacleClient>().ToITentacleTaskLog().Chain(log),
                token).ConfigureAwait(false);
        }

        // Some integration tests need to invoke CancelScript / GetStatus directly against an
        // already-running ScriptServiceV2 script without going through ExecuteScript. They have
        // a ScriptTicket but not a CommandContext (which TentacleClient's high-level methods
        // expect). These helpers synthesize a CommandContext from the ticket.
        public static async Task<ScriptStatus> CancelScript(
            this TentacleClient tentacleClient,
            ScriptTicket scriptTicket,
            ITentacleClientTaskLog? log = null)
        {
            var commandContext = new CommandContext(scriptTicket, 0, ScriptServiceVersion.ScriptServiceVersion2);
            var result = await tentacleClient.CancelScript(commandContext,
                new SerilogLoggerBuilder().Build().ForContext<TentacleClient>().ToITentacleTaskLog().Chain(log))
                .ConfigureAwait(false);
            return result.ScriptStatus;
        }

        public static async Task<ScriptStatus> GetStatus(
            this TentacleClient tentacleClient,
            ScriptTicket scriptTicket,
            CancellationToken token,
            ITentacleClientTaskLog? log = null)
        {
            var commandContext = new CommandContext(scriptTicket, 0, ScriptServiceVersion.ScriptServiceVersion2);
            var result = await tentacleClient.GetStatus(commandContext,
                new SerilogLoggerBuilder().Build().ForContext<TentacleClient>().ToITentacleTaskLog().Chain(log),
                token).ConfigureAwait(false);
            return result.ScriptStatus;
        }

        public static async Task<DataStream> DownloadFile(
            this TentacleClient tentacleClient,
            string remotePath,
            CancellationToken token,
            ITentacleClientTaskLog? log = null)
        {
            var result = await tentacleClient.DownloadFile(remotePath,
                new SerilogLoggerBuilder().Build().ForContext<TentacleClient>().ToITentacleTaskLog().Chain(log),
                token).ConfigureAwait(false);

            return result;
        }
    }
}