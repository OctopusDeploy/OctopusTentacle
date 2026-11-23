using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Client.Scripts.Models.Builders;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Docker.Tests.Integration.Setup;
using Octopus.Tentacle.Tests.Integration.Common.FakeOctopus;

namespace Octopus.Tentacle.Docker.Tests.Integration;

/// <summary>
/// A container started with ServerThumbprint listens for that Octopus Server without registering with it, for when the
/// Tentacle cannot reach the server (https://github.com/OctopusDeploy/OctopusTentacle/issues/456). The user adds it to
/// Octopus by hand, using the thumbprint it prints.
/// </summary>
public class LinuxTentacleUnregisteredTests
{
    static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(30);
    static readonly TimeSpan DockerCommandTimeout = TimeSpan.FromMinutes(2);

    // What configure-tentacle.sh exits with when the environment variables are invalid.
    const int ConfigurationErrorExitCode = 1;

    [Test]
    public async Task TentacleRunsScriptsForTheTrustedServerWithoutRegistering()
    {
        await using var server = await new FakeOctopusServerBuilder().Build();
        await using var tentacle = await new TentacleContainerBuilder(server).WithoutRegistration(TestCertificates.ServerPublicThumbprint).Build();

        var (result, output) = await tentacle.ExecuteScript(EchoScript("running without registering"));

        result.State.Should().Be(ProcessState.Complete);
        result.ExitCode.Should().Be(0);
        output.Should().Contain("running without registering");
        server.RegistrationCount(FakeOctopusServerBuilder.SpaceName, tentacle.MachineName).Should().Be(0);
        server.RequestLog.Should().BeEmpty("a Tentacle that does not register should never contact the server's API");
    }

    [Test]
    public async Task TentacleTrustsOnlyTheGivenServer()
    {
        await using var server = await new FakeOctopusServerBuilder().Build();
        await using var tentacle = await new TentacleContainerBuilder(server).WithoutRegistration(TestCertificates.ServerPublicThumbprint).Build();

        // BeEquivalentTo ignores case: thumbprints are hexadecimal, so the case they are written in doesn't matter.
        (await TrustedThumbprints(tentacle)).Should().ContainSingle().Which.Should().BeEquivalentTo(TestCertificates.ServerPublicThumbprint);
    }

    /// <summary>
    /// With a known certificate, its thumbprint can be added to Octopus before the container starts, and stays the same
    /// if the container is recreated without its volume.
    /// </summary>
    [Test]
    public async Task TentacleUsesTheGivenCertificate()
    {
        var certificateBase64 = Convert.ToBase64String(await File.ReadAllBytesAsync(TestCertificates.TentaclePfxPath));

        await using var server = await new FakeOctopusServerBuilder().Build();
        await using var tentacle = await new TentacleContainerBuilder(server)
            .WithoutRegistration(TestCertificates.ServerPublicThumbprint)
            .WithCertificate(certificateBase64)
            .Build();

        tentacle.Thumbprint.Should().BeEquivalentTo(TestCertificates.TentaclePublicThumbprint);
        var (result, output) = await tentacle.ExecuteScript(EchoScript("running with the given certificate"));
        result.ExitCode.Should().Be(0);
        output.Should().Contain("running with the given certificate");

        // The certificate includes its private key, so scripts must not be able to read it either.
        await tentacle.AssertRegistrationCredentialsAreNotVisibleToScripts();
    }

    [Test]
    public async Task ARestartedContainerPrintsItsThumbprintAgainAndRunsScripts()
    {
        await using var server = await new FakeOctopusServerBuilder().Build();
        await using var tentacle = await new TentacleContainerBuilder(server).WithoutRegistration(TestCertificates.ServerPublicThumbprint).Build();
        var thumbprint = tentacle.Thumbprint;

        await tentacle.Restart(GracePeriod);

        var (result, output) = await tentacle.ExecuteScript(EchoScript("running after a restart"));
        result.ExitCode.Should().Be(0);
        output.Should().Contain("running after a restart");

        var logs = await tentacle.Docker.GetLogs();
        Regex.Matches(logs, $"listening Tentacle with thumbprint {thumbprint}").Should().HaveCount(2, "the thumbprint is printed on every start, not only the first");
    }

    [Test]
    [TestCase("ServerUrl", "https://octopus.example.com")]
    [TestCase("ServerApiKey", "API-NOTAREALKEY")]
    [TestCase("ServerCommsAddress", "https://octopus.example.com")]
    [TestCase("ServerPort", "10943")]
    [TestCase("TargetRole", "web-server")]
    [TestCase("TargetWorkerPool", "Default Worker Pool")]
    public async Task RegistrationSettingsAreRejected(string variable, string value)
    {
        await using var server = await new FakeOctopusServerBuilder().Build();

        var (exitCode, logs) = await new TentacleContainerBuilder(server)
            .WithoutRegistration(TestCertificates.ServerPublicThumbprint)
            .WithEnvironmentVariable(variable, value)
            .RunUntilExit();

        exitCode.Should().Be(ConfigurationErrorExitCode);
        logs.Should().Contain($"The '{variable}' environment variable can't be used with 'ServerThumbprint'");
    }

    [Test]
    [TestCase("not-a-thumbprint")]
    [TestCase("ABCDEF")]
    public async Task AnInvalidServerThumbprintIsRejected(string serverThumbprint)
    {
        await using var server = await new FakeOctopusServerBuilder().Build();

        var (exitCode, logs) = await new TentacleContainerBuilder(server).WithoutRegistration(serverThumbprint).RunUntilExit();

        exitCode.Should().Be(ConfigurationErrorExitCode);
        logs.Should().Contain("The 'ServerThumbprint' environment variable must be the 40 character hexadecimal thumbprint");
    }

    /// <summary>A registering Tentacle can import a known certificate too.</summary>
    [Test]
    public async Task ARegisteringTentacleUsesTheGivenCertificate()
    {
        var certificateBase64 = Convert.ToBase64String(await File.ReadAllBytesAsync(TestCertificates.TentaclePfxPath));

        await using var server = await new FakeOctopusServerBuilder().Build();
        await using var tentacle = await new TentacleContainerBuilder(server).WithCertificate(certificateBase64).Build();

        tentacle.RegisteredMachine.Thumbprint.Should().BeEquivalentTo(TestCertificates.TentaclePublicThumbprint);
    }

    static ExecuteScriptCommand EchoScript(string message)
        => new ExecuteShellScriptCommandBuilder(LoggingUtils.CurrentTestHash(), ScriptIsolationLevel.NoIsolation)
            .WithScriptBody($"echo '{message}'\n")
            .Build();

    /// <summary>The thumbprints of the Octopus Servers the Tentacle trusts, read from its configuration file.</summary>
    static async Task<string[]> TrustedThumbprints(TentacleContainer tentacle)
    {
        using var cancellationTokenSource = new CancellationTokenSource(DockerCommandTimeout);
        var config = XDocument.Parse(await tentacle.Docker.Exec(new[] { "cat", "/etc/octopus/tentacle.config" }, cancellationTokenSource.Token));
        var trustedServers = config.Descendants("set").Single(e => (string?)e.Attribute("key") == "Tentacle.Communication.TrustedOctopusServers").Value;

        using var json = JsonDocument.Parse(trustedServers);
        return json.RootElement.EnumerateArray().Select(s => s.GetProperty("Thumbprint").GetString()!).ToArray();
    }
}
