#nullable enable
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.TestAttributes;
using Octopus.Tentacle.Tests.Integration.Util.Builders;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class TentacleStartupAndShutdownTests : IntegrationTest
    {
        [Test]
        [TentacleConfigurations]
        [RequiresSudoOnLinux]
        public async Task WhenRunningTentacleAsAServiceItShouldBeAbleToRestartItself(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using (var clientAndTentacle = await tentacleConfigurationTestCase
                             .CreateBuilder()
                             .InstallAsAService()
                             .Build(CancellationToken))
            {
                var startScriptCommand = new LatestStartScriptCommandBuilder()                    
                    .WithScriptBodyForCurrentOs(
$@"cd ""{clientAndTentacle.RunningTentacle.TentacleExe.DirectoryName}""
.\Tentacle.exe service --instance {clientAndTentacle.RunningTentacle.InstanceName} --stop --start",
$@"#!/bin/sh
cd ""{clientAndTentacle.RunningTentacle.TentacleExe.DirectoryName}""
./Tentacle service --instance {clientAndTentacle.RunningTentacle.InstanceName} --stop --start")
                    .Build();

                var result = await clientAndTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);

                var scriptOutput = new StringBuilder();
                result.ProcessOutput.ForEach(x => scriptOutput.AppendLine($"{x.Source} {x.Occurred} {x.Text}"));

                Logger.Information($@"Script Output:
{scriptOutput}");

                result.ProcessOutput.Any(x => x.Text.Contains("Stopping service")).Should().BeTrue("Stopping service should be logged");
                result.ScriptExecutionResult.State.Should().Be(ProcessState.Complete);

                startScriptCommand = new LatestStartScriptCommandBuilder()                    
                    .WithScriptBody(new ScriptBuilder().Print("Running..."))
                    .Build();

                result = await clientAndTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);
                result.ProcessOutput.Any(x => x.Text.Contains("Running...")).Should().BeTrue("Running... should be logged");
                result.ScriptExecutionResult.ExitCode.Should().Be(0);
                result.ScriptExecutionResult.State.Should().Be(ProcessState.Complete);
            }
        }
    }
}
