using System.Diagnostics;
using FluentAssertions;
using Octopus.Tentacle.Client.Scripts.Models.Builders;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Docker.Tests.Integration.Setup;

namespace Octopus.Tentacle.Docker.Tests.Integration;

/// <summary>
/// What happens to a running Tentacle container when the host restarts or stops it. These change the container, so
/// they have their own rather than sharing LinuxTentacleImageTests', and run in order: restart, then stop.
/// </summary>
[TestFixture(TentacleCommunicationStyle.Listening)]
[TestFixture(TentacleCommunicationStyle.Polling)]
public class LinuxTentacleContainerLifecycleTests : TentacleContainerFixture
{
    // docker stop and docker restart send SIGTERM, then SIGKILL once this has passed.
    static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(30);
    static readonly TimeSpan DockerCommandTimeout = TimeSpan.FromMinutes(2);

    // What configure-tentacle.sh prints when it finds the semaphore it writes after a successful registration.
    const string AlreadyConfiguredMessage = "Octopus Tentacle is already configured. Skipping reconfiguration.";

    // SIGKILL: docker stop ran out of patience.
    const int KilledExitCode = 137;

    public LinuxTentacleContainerLifecycleTests(TentacleCommunicationStyle communicationStyle) : base(communicationStyle, "Lifecycle")
    {
    }

    /// <summary>
    /// A restarted container, e.g. after the host reboots, keeps its configuration: it must not register again, and
    /// must come back with the same certificate and trust so the server can still run scripts on it.
    /// </summary>
    [Test]
    [Order(1)]
    public async Task ARestartedContainerKeepsItsRegistrationAndRunsScripts()
    {
        var server = DockerTestsSetUpFixture.Server;
        server.RegistrationCount(SpaceName, MachineName).Should().Be(1);

        using (var cancellationTokenSource = new CancellationTokenSource(DockerCommandTimeout))
        {
            await Container.Restart(GracePeriod, cancellationTokenSource.Token);
        }

        await RebuildTentacleClient();

        var command = new ExecuteShellScriptCommandBuilder(LoggingUtils.CurrentTestHash(), ScriptIsolationLevel.NoIsolation)
            .WithScriptBody("echo 'running after a restart'\n")
            .Build();

        var (result, output) = await ExecuteScript(command);

        result.ExitCode.Should().Be(0);
        output.Should().Contain("running after a restart");
        server.RegistrationCount(SpaceName, MachineName).Should().Be(1, "a restarted container is already configured and should not register again");
        (await Container.GetLogs()).Should().Contain(AlreadyConfiguredMessage);
    }

    /// <summary>
    /// docker stop sends SIGTERM and kills the container if it has not exited by the end of the grace period. Tentacle
    /// should shut down on SIGTERM, so orchestrators are not left waiting and Tentacle is not killed mid-shutdown.
    /// </summary>
    [Test]
    [Order(2)]
    public async Task StoppingTheContainerShutsTentacleDownWithoutBeingKilled()
    {
        using var cancellationTokenSource = new CancellationTokenSource(DockerCommandTimeout);

        var stopwatch = Stopwatch.StartNew();
        await Container.Stop(GracePeriod, cancellationTokenSource.Token);
        stopwatch.Stop();

        var exitCode = await Container.GetExitCode(cancellationTokenSource.Token);
        TestContext.Out.WriteLine($"docker stop took {stopwatch.Elapsed.TotalSeconds:0.0}s; the container exited with code {exitCode}");

        exitCode.Should().NotBe(KilledExitCode, "Tentacle should exit on SIGTERM rather than be killed when the grace period runs out");
        stopwatch.Elapsed.Should().BeLessThan(GracePeriod);
    }
}
