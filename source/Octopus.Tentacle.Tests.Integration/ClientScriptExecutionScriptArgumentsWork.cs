using System;
using System.Collections;
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
    [Parallelizable(scope: ParallelScope.All)]
    public class ClientScriptExecutionScriptArgumentsWork : IntegrationTest
    {
        class AllTentacleTypesWithV1V2ScriptServices : IEnumerable
        {
            public IEnumerator GetEnumerator()
            {
                return CartesianProduct.Of(new TentacleTypesToTest(), new V1OnlyAndV2ScriptServiceTentacleVersions()).GetEnumerator();
            }
        }

        [Test]
        [TestCaseSource(typeof(AllTentacleTypesWithV1V2ScriptServices))]
        public async Task ArgumentsArePassedToTheScript(TentacleType tentacleType, string tentacleVersion)
        {
            var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithTentacleVersion(tentacleVersion)
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder().PrintArguments())
                .WithArguments("First", "Second", "AndSpacesAreNotHandledWellInTentacle")
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