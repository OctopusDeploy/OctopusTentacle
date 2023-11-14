using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.ExtensionMethods;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders
{
    public static class TentacleClientExtensionMethods
    {
        public static async Task<(ScriptExecutionResult, List<ProcessOutput>)> ExecuteScript(
            this TentacleClient tentacleClient,
            StartScriptCommandV3Alpha startScriptCommand,
            CancellationToken token,
            OnScriptStatusResponseReceived? onScriptStatusResponseReceivedAction = null,
            Log? log = null)
        {
            var logs = new List<ProcessOutput>();
            var finalResponse = await tentacleClient.ExecuteScript(startScriptCommand,
                onScriptStatusResponseReceived =>
                {
                    if (onScriptStatusResponseReceivedAction != null)
                    {
                        onScriptStatusResponseReceivedAction(onScriptStatusResponseReceived);
                    }

                    logs.AddRange(onScriptStatusResponseReceived.Logs);
                },
                cts => Task.CompletedTask,
                new SerilogLoggerBuilder().Build().ForContext<TentacleClient>().ToILog().Chain(log),
                token).ConfigureAwait(false);
            return (finalResponse, logs);
        }

        public static async Task<UploadResult> UploadFile(
            this TentacleClient tentacleClient,
            string remotePath,
            DataStream upload,
            CancellationToken token,
            Log? log = null)
        {
            var result = await tentacleClient.UploadFile(Path.GetFileName(remotePath),
                remotePath,
                upload,
                new SerilogLoggerBuilder().Build().ForContext<TentacleClient>().ToILog().Chain(log),
                token).ConfigureAwait(false);

            return result;
        }

        public static async Task<DataStream> DownloadFile(
            this TentacleClient tentacleClient,
            string remotePath,
            CancellationToken token,
            Log? log = null)
        {
            var result = await tentacleClient.DownloadFile(remotePath,
                new SerilogLoggerBuilder().Build().ForContext<TentacleClient>().ToILog().Chain(log),
                token).ConfigureAwait(false);

            return result;
        }
    }
}