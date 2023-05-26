using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;

namespace Octopus.Tentacle.Tests.Integration
{
    [RunTestsInParallelLocallyIfEnabledButNeverOnTeamCity]
    public class ClientScriptExecutionScriptFilesAreSent : IntegrationTest
    {
        [Test]
        [TestCaseSource(typeof(AllTentacleTypesWithV1V2ScriptServices))]
        public async Task ArgumentsArePassedToTheScript(TentacleType tentacleType, string tentacleVersion)
        {
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithTentacleVersion(tentacleVersion)
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder().PrintFileContents("foo.txt"))
                .WithFiles(new ScriptFile("foo.txt", DataStream.FromString("The File Contents")))
                .Build();

            var (finalResponse, logs) = await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);

            var allLogs = logs.JoinLogs();

            allLogs.Should().Contain("The File Contents");
        }
    }
}