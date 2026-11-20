using FluentAssertions;
using Octopus.Tentacle.Client.Scripts.Models.Builders;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Docker.Tests.Integration.Setup;
using Octopus.Tentacle.Tests.Integration.Util.Builders;

namespace Octopus.Tentacle.Docker.Tests.Integration;

/// <summary>
/// Runs the Linux Tentacle image the way customers do, registers it with a fake Octopus Server, then runs scripts on
/// it over Halibut.
/// </summary>
public class LinuxTentacleImageTests
{
    [Test]
    [TestCase(TentacleCommunicationStyle.Listening)]
    [TestCase(TentacleCommunicationStyle.Polling)]
    public async Task TentacleRegistersItselfFromTheEnvironmentVariables(TentacleCommunicationStyle communicationStyle)
    {
        await using var server = await new FakeOctopusServerBuilder().Build();
        await using var tentacle = await new TentacleContainerBuilder(server).WithCommunicationStyle(communicationStyle).Build();

        var machine = tentacle.RegisteredMachine;
        machine.Name.Should().Be(tentacle.MachineName);
        machine.Roles.Should().Equal(TentacleContainerBuilder.Role);
        machine.EnvironmentIds.Should().HaveCount(1);
        machine.Thumbprint.Should().NotBeNullOrWhiteSpace();
        machine.IsWorker.Should().BeFalse();
        machine.Authentication.Should().Be($"api key {TentacleContainerBuilder.ApiKey}");

        if (communicationStyle == TentacleCommunicationStyle.Listening)
        {
            machine.CommunicationStyle.Should().Be("TentaclePassive");
            // The image defaults PublicHostNameConfiguration to ComputerName, and the container's hostname is its name.
            machine.Uri.Should().Be($"https://{tentacle.MachineName}:{TentacleContainer.ListeningPort}/");
        }
        else
        {
            machine.CommunicationStyle.Should().Be("TentacleActive");
            machine.Uri.Should().StartWith("poll://");
        }
    }

    [Test]
    [TestCase(TentacleCommunicationStyle.Listening)]
    [TestCase(TentacleCommunicationStyle.Polling)]
    public async Task ScriptsRunAndTheirOutputIsReturned(TentacleCommunicationStyle communicationStyle)
    {
        await using var server = await new FakeOctopusServerBuilder().Build();
        await using var tentacle = await new TentacleContainerBuilder(server).WithCommunicationStyle(communicationStyle).Build();

        var command = new ExecuteShellScriptCommandBuilder(LoggingUtils.CurrentTestHash(), ScriptIsolationLevel.NoIsolation)
            .WithScriptBody(new ScriptBuilder()
                .Print("Hello from the Tentacle container")
                .BuildBashScript() + "\necho \"Running on $(. /etc/os-release && echo \"$ID $VERSION_ID\")\"\n")
            .Build();

        var (result, output) = await tentacle.ExecuteScript(command);

        result.State.Should().Be(ProcessState.Complete);
        result.ExitCode.Should().Be(0);
        output.Should().Contain("Hello from the Tentacle container");
        output.Should().Contain("Running on ubuntu");
    }
}
