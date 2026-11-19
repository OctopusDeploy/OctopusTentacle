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
[TestFixture(TentacleCommunicationStyle.Listening)]
[TestFixture(TentacleCommunicationStyle.Polling)]
public class LinuxTentacleImageTests : TentacleContainerFixture
{
    public LinuxTentacleImageTests(TentacleCommunicationStyle communicationStyle) : base(communicationStyle, "Docker")
    {
    }

    [Test]
    public void TentacleRegistersItselfFromTheEnvironmentVariables()
    {
        RegisteredMachine.Name.Should().Be(MachineName);
        RegisteredMachine.Roles.Should().Equal(Role);
        RegisteredMachine.EnvironmentIds.Should().HaveCount(1);
        RegisteredMachine.Thumbprint.Should().NotBeNullOrWhiteSpace();
        RegisteredMachine.IsWorker.Should().BeFalse();
        RegisteredMachine.Authentication.Should().Be($"api key {ApiKey}");

        if (CommunicationStyle == TentacleCommunicationStyle.Listening)
        {
            RegisteredMachine.CommunicationStyle.Should().Be("TentaclePassive");
            // The image defaults PublicHostNameConfiguration to ComputerName, and the container's hostname is its name.
            RegisteredMachine.Uri.Should().Be($"https://{MachineName}:{TentacleContainer.ListeningPort}/");
        }
        else
        {
            RegisteredMachine.CommunicationStyle.Should().Be("TentacleActive");
            RegisteredMachine.Uri.Should().StartWith("poll://");
        }
    }

    [Test]
    public async Task ScriptsRunAndTheirOutputIsReturned()
    {
        var command = new ExecuteShellScriptCommandBuilder(LoggingUtils.CurrentTestHash(), ScriptIsolationLevel.NoIsolation)
            .WithScriptBody(new ScriptBuilder()
                .Print("Hello from the Tentacle container")
                .BuildBashScript() + "\necho \"Running on $(. /etc/os-release && echo \"$ID $VERSION_ID\")\"\n")
            .Build();

        var (result, output) = await ExecuteScript(command);

        result.State.Should().Be(ProcessState.Complete);
        result.ExitCode.Should().Be(0);
        output.Should().Contain("Hello from the Tentacle container");
        output.Should().Contain("Running on ubuntu");
    }

    /// <summary>
    /// The container is started with ServerApiKey; LinuxTentacleAuthenticationTests covers the others. The entrypoint
    /// unsets credentials the same way however Tentacle communicates, so checking one style is enough.
    /// </summary>
    [Test]
    public async Task RegistrationCredentialsAreNotVisibleToScripts()
    {
        if (CommunicationStyle != TentacleCommunicationStyle.Listening)
        {
            Assert.Ignore("Unsetting credentials does not depend on the communication style; the listening fixture checks it.");
        }

        await AssertRegistrationCredentialsAreNotVisibleToScripts();
    }
}
