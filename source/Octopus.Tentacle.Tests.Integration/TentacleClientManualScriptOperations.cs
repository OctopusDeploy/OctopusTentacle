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

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class TentacleClientManualScriptOperations : IntegrationTest
    {
        [Test]
        [TentacleConfigurations(testCommonVersions: true)]
        public async Task CanStart_GetStatus_AndFinishScriptsManually(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder().Build(CancellationToken);

            var waitForFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "waitforme");

            var startScriptCommand = new LatestStartScriptCommandBuilder()
                .WithScriptBody(b => b.Print("hello").WaitForFileToExist(waitForFile))
                .Build();

            // Can Start
            var response = await clientTentacle.TentacleClient.StartScript(startScriptCommand, CancellationToken);
            response.State.Should().NotBe(ProcessState.Complete);
            
            // Can Get Status while Running
            response = await clientTentacle.TentacleClient.GetStatus(response.NextCommandContext, CancellationToken);
            response.State.Should().NotBe(ProcessState.Complete);

            // Can Get Status After Completed
            await File.WriteAllTextAsync(waitForFile, "", CancellationToken);
            await Wait.For(
                async () => (await clientTentacle.TentacleClient.GetStatus(response.NextCommandContext, CancellationToken)).State == ProcessState.Complete,
                TimeSpan.FromSeconds(60),
                () => throw new Exception("Script Execution did not complete"),
                CancellationToken);
            
            // Can Finish
            await clientTentacle.TentacleClient.Finish(response.NextCommandContext, CancellationToken);
        }

        [Test]
        [TentacleConfigurations(testCommonVersions: true)]
        public async Task CanStart_Cancel_AndFinishScriptsManually(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder().Build(CancellationToken);

            var waitForFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "waitforme");

            var startScriptCommand = new LatestStartScriptCommandBuilder()
                .WithScriptBody(b => b.Print("hello").WaitForFileToExist(waitForFile))
                .Build();

            // Start
            var response = await clientTentacle.TentacleClient.StartScript(startScriptCommand, CancellationToken);
            response.State.Should().NotBe(ProcessState.Complete);

            // Cancel
            response = await clientTentacle.TentacleClient.Cancel(response.NextCommandContext, CancellationToken);

            // Wait until completed.
            await Wait.For(
                async () => (await clientTentacle.TentacleClient.GetStatus(response.NextCommandContext, CancellationToken)).State == ProcessState.Complete,
                TimeSpan.FromSeconds(60),
                () => throw new Exception("Script Execution did not complete"),
                CancellationToken);

            // Finish
            await clientTentacle.TentacleClient.Finish(response.NextCommandContext, CancellationToken);
        }
    }
}
