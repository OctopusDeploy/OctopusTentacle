#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Exceptions;
using NUnit.Framework;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.ExtensionMethods;
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
                
                var startScriptCommand = new ExecuteScriptCommandBuilder()                    
                    .WithScriptBodyForCurrentOs(
$@"cd ""{clientAndTentacle.RunningTentacle.TentacleExe.DirectoryName}""
.\Tentacle.exe service --instance {clientAndTentacle.RunningTentacle.InstanceName} --stop --start",
$@"#!/bin/sh
cd ""{clientAndTentacle.RunningTentacle.TentacleExe.DirectoryName}""
./Tentacle service --instance {clientAndTentacle.RunningTentacle.InstanceName} --stop --start")
                    .Build();

                (ScriptExecutionResult ScriptExecutionResult, List<ProcessOutput> ProcessOutput) result;

                try
                {
                    result = await clientAndTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);
                }
                catch (ServiceInvocationHalibutClientException ex)
                {
                    Logger.Information(ex, "ServiceInvocationHalibutClientException thrown while Tentacle was restarting itself. This can be ignored for the purpose of this test.");

                    // Making Tentacle restart itself can cause internal errors with Script Service
                    // Execute the script again to get the final result and logs. This will not rerun the script.
                    result = await clientAndTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);
                }

                result.LogExecuteScriptOutput(Logger);

                result.ProcessOutput.Any(x => x.Text.Contains("Stopping service")).Should().BeTrue("Stopping service should be logged");
                result.ScriptExecutionResult.State.Should().Be(ProcessState.Complete);

                startScriptCommand = new ExecuteScriptCommandBuilder()                    
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
