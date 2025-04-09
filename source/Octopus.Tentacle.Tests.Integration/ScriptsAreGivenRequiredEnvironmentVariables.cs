using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Client.Extensions;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class ScriptsAreGivenRequiredEnvironmentVariables : IntegrationTest
    {
        [Test]
        [TentacleConfigurations(testPolling: false)]
        public async Task EnsureEnvironmentVariablesAreSetForScripts(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var recordedUsages)
                    .Build())
                .Build(CancellationToken);

            var script = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder()
                    .PrintEnv())
                .Build();
            
            var tentacleClient = clientTentacle.TentacleClient;
            var scriptResult = await tentacleClient.ExecuteScript(script, CancellationToken);

            var scriptOutput = scriptResult.ProcessOutput.Select(p => p.Text).StringJoin("\r\n");

            scriptOutput.Should().Contain("TentacleHome");
            scriptOutput.Should().Contain("CalamariPackageRetentionJournalPath");
            scriptOutput.Should().Contain("TentacleJournal");
        }
    }
}
