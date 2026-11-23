using System.Diagnostics;
using FluentAssertions;
using Octopus.Tentacle.Client.Scripts.Models.Builders;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Docker.Tests.Integration.Setup;
using Octopus.Tentacle.Tests.Integration.Common.FakeOctopus;

namespace Octopus.Tentacle.Docker.Tests.Integration;

/// <summary>What happens to a running Tentacle container when the host restarts or stops it.</summary>
public class LinuxTentacleContainerLifecycleTests
{
    // docker stop and docker restart send SIGTERM, then SIGKILL once this has passed.
    static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(30);
    static readonly TimeSpan DockerCommandTimeout = TimeSpan.FromMinutes(2);

    // What configure-tentacle.sh prints when it finds the semaphore it writes after a successful registration.
    const string AlreadyConfiguredMessage = "Octopus Tentacle is already configured. Skipping reconfiguration.";

    // SIGKILL: docker stop ran out of patience.
    const int KilledExitCode = 137;

    /// <summary>
    /// A restarted container, e.g. after the host reboots, keeps its configuration: it must not register again, and
    /// must come back with the same certificate and trust so the server can still run scripts on it.
    /// </summary>
    [Test]
    [TestCase(TentacleCommunicationStyle.Listening)]
    [TestCase(TentacleCommunicationStyle.Polling)]
    public async Task ARestartedContainerKeepsItsRegistrationAndRunsScripts(TentacleCommunicationStyle communicationStyle)
    {
        await using var server = await new FakeOctopusServerBuilder().Build();
        await using var tentacle = await new TentacleContainerBuilder(server).WithCommunicationStyle(communicationStyle).Build();
        server.RegistrationCount(FakeOctopusServerBuilder.SpaceName, tentacle.MachineName).Should().Be(1);

        await tentacle.Restart(GracePeriod);

        var command = new ExecuteShellScriptCommandBuilder(LoggingUtils.CurrentTestHash(), ScriptIsolationLevel.NoIsolation)
            .WithScriptBody("echo 'running after a restart'\n")
            .Build();

        var (result, output) = await tentacle.ExecuteScript(command);

        result.ExitCode.Should().Be(0);
        output.Should().Contain("running after a restart");
        server.RegistrationCount(FakeOctopusServerBuilder.SpaceName, tentacle.MachineName).Should().Be(1, "a restarted container is already configured and should not register again");
        (await tentacle.Docker.GetLogs()).Should().Contain(AlreadyConfiguredMessage);
    }

    /// <summary>
    /// docker stop sends SIGTERM and kills the container if it has not exited by the end of the grace period. Tentacle
    /// should shut down on SIGTERM, so orchestrators are not left waiting and Tentacle is not killed mid-shutdown.
    /// </summary>
    [Test]
    [TestCase(TentacleCommunicationStyle.Listening)]
    [TestCase(TentacleCommunicationStyle.Polling)]
    public async Task StoppingTheContainerShutsTentacleDownWithoutBeingKilled(TentacleCommunicationStyle communicationStyle)
    {
        await using var server = await new FakeOctopusServerBuilder().Build();
        await using var tentacle = await new TentacleContainerBuilder(server).WithCommunicationStyle(communicationStyle).Build();
        using var cancellationTokenSource = new CancellationTokenSource(DockerCommandTimeout);

        // Registration completes before the agent starts. Tentacle is the container's PID 1, and the kernel drops
        // signals PID 1 has no handler for, so a SIGTERM sent before .NET has installed its handler is lost and docker
        // stop waits out the grace period. Running a script shows the agent is up before stopping it.
        var command = new ExecuteShellScriptCommandBuilder(LoggingUtils.CurrentTestHash(), ScriptIsolationLevel.NoIsolation)
            .WithScriptBody("echo 'agent is running'\n")
            .Build();
        var (result, _) = await tentacle.ExecuteScript(command);
        result.ExitCode.Should().Be(0);

        var stopwatch = Stopwatch.StartNew();
        await tentacle.Docker.Stop(GracePeriod, cancellationTokenSource.Token);
        stopwatch.Stop();

        var exitCode = await tentacle.Docker.GetExitCode(cancellationTokenSource.Token);
        TestContext.Out.WriteLine($"docker stop took {stopwatch.Elapsed.TotalSeconds:0.0}s; the container exited with code {exitCode}");

        exitCode.Should().NotBe(KilledExitCode, "Tentacle should exit on SIGTERM rather than be killed when the grace period runs out");
        stopwatch.Elapsed.Should().BeLessThan(GracePeriod);
    }
}
