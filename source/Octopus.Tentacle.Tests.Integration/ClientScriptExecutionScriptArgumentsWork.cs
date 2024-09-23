using System;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class ClientScriptExecutionScriptArgumentsWork : IntegrationTest
    {
        [Test]
        [TentacleConfigurations(testCommonVersions: true)]
        public async Task ArgumentsArePassedToTheScript(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder().Build(CancellationToken);

            var startScriptCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder().PrintArguments())
                .WithArguments(new[] { "First", "Second", "AndSpacesAreNotHandledWellInTentacle" })
                .Build();

            var tentacleServicesDecorator = new TentacleServiceDecoratorBuilder().Build();

            var (finalResponse, logs) = await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);

            var allLogs = logs.JoinLogs();

            allLogs.Should().MatchRegex(".*Argument: First\n.*Argument: Second\n.*Argument: AndSpacesAreNotHandledWellInTentacle\n");
        }
    }
}