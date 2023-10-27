using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class ClientScriptExecutionAdditionalScripts : IntegrationTest
    {
        [Test]
        [TentacleConfigurations(testCommonVersions: true)]
        public async Task AdditionalScriptsWork(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            using var tmp = new TemporaryDirectory();
            var path = Path.Combine(tmp.DirectoryPath, "file");

            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .CountCallsToScriptServiceV2(out var scriptServiceV2CallCounts)
                    .Build())
                .Build(CancellationToken);

            var scriptBuilder = new ScriptBuilder()
                .CreateFile(path) // How files are made are different in bash and powershell, doing this ensures the client and tentacle really are using the correct script.
                .Print("Hello");

            var startScriptCommand = new StartScriptCommandV3AlphaBuilder()
                .WithAdditionalScriptTypes(ScriptType.Bash, scriptBuilder.BuildBashScript())
                // Additional Scripts don't actually work on tentacle for anything other than bash.
                // Below is what we would have expected to tentacle to work with.
                //.WithAdditionalScriptTypes(ScriptType.PowerShell, scriptBuilder.BuildPowershellScript())
                // But instead we need to send the powershell in the scriptbody.
                .WithScriptBody(scriptBuilder.BuildPowershellScript())
                .Build();

            var (finalResponse, logs) = await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);

            var allLogs = logs.JoinLogs();

            allLogs.Should().Contain("Hello");
        }
    }
}
