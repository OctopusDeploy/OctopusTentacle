﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;
using Octopus.Tentacle.Tests.Integration.Support;

namespace Octopus.Tentacle.Tests.Integration.Util
{
    static class TentacleClientTestExtensionMethods
    {
        public static async Task ExecuteScript(
            this TentacleClient tentacleClient,
            ExecuteScriptCommand executeScriptCommand,
            List<ProcessOutput> logs,
            CancellationToken token)
        {
            await tentacleClient.ExecuteScript(executeScriptCommand,
                onScriptStatusResponseReceived => logs.AddRange(onScriptStatusResponseReceived.Logs),
                cts => Task.CompletedTask,
                new SerilogLoggerBuilder().Build().ForContext<TentacleClient>().ToILog(),
                token);
        }
    }
}
