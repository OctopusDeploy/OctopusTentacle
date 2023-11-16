using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;
using Octopus.Tentacle.Tests.Integration.Support;

namespace Octopus.Tentacle.Tests.Integration.Util
{
    static class TentacleClientTestExtensionMethods
    {
        public static async Task ExecuteScript(
            this TentacleClient tentacleClient,
            StartScriptCommandV3Alpha startScriptCommand,
            List<ProcessOutput> logs,
            CancellationToken token)
        {
            await tentacleClient.ExecuteScript(startScriptCommand,
                onScriptStatusResponseReceived => logs.AddRange(onScriptStatusResponseReceived.Logs),
                cts => Task.CompletedTask,
                new SerilogLoggerBuilder().Build().ForContext<TentacleClient>().ToILog(),
                token);
        }
    }
}
