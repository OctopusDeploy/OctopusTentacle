using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Tests.Integration.Support;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders
{
    public static class TentacleClientExtensionMethods
    {
        public static async Task<(ScriptStatusResponseV2, List<ProcessOutput>)> ExecuteScript(
            this TentacleClient tentacleClient,
            StartScriptCommandV2 startScriptCommand,
            CancellationToken token,
            Action<ScriptStatusResponseV2> onScriptStatusResponseReceivedAction = null)
        {
            var logs = new List<ProcessOutput>();
            var finalResponse = await tentacleClient.ExecuteScript(startScriptCommand,
                onScriptStatusResponseReceived =>
                {
                    if(onScriptStatusResponseReceivedAction != null) onScriptStatusResponseReceivedAction(onScriptStatusResponseReceived);
                    logs.AddRange(onScriptStatusResponseReceived.Logs);
                },
                cts => Task.CompletedTask,
                new SerilogLoggerBuilder().Build().ForContext<TentacleClient>().ToILog(),
                token);
            return (finalResponse, logs);
        }

        public static async Task<UploadResult> UploadFile(
            this TentacleClient tentacleClient,
            string remotePath,
            DataStream upload,
            CancellationToken token)
        {
            var result = await tentacleClient.UploadFile(Path.GetFileName(remotePath),
                remotePath,
                upload,
                new SerilogLoggerBuilder().Build().ForContext<TentacleClient>().ToILog(),
                token);

            return result;
        }

        public static async Task<DataStream> DownloadFile(
            this TentacleClient tentacleClient,
            string remotePath,
            CancellationToken token)
        {
            var result = await tentacleClient.DownloadFile(remotePath,
                new SerilogLoggerBuilder().Build().ForContext<TentacleClient>().ToILog(),
                token);

            return result;
        }
    }
}