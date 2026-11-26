using FluentAssertions;
using Octopus.Tentacle.Docker.Tests.Integration.Setup;
using Octopus.Tentacle.Tests.Integration.Common.FakeOctopus;

namespace Octopus.Tentacle.Docker.Tests.Integration;

/// <summary>
/// Docker-in-Docker is on by default and needs a privileged container. The tests run containers unprivileged, so they
/// can check what happens when it can't start (https://github.com/OctopusDeploy/OctopusTentacle/issues/323); the image
/// smoke tests cover it starting.
/// </summary>
public class LinuxTentacleDockerInDockerTests
{
    // SIGTERM: what the container exited with when Tentacle was stopped because Docker-in-Docker had.
    const int TerminatedExitCode = 143;

    /// <summary>
    /// The container must stop before registering, so Octopus isn't left with a target that never comes online, and
    /// say how to fix it.
    /// </summary>
    [Test]
    public async Task AContainerThatCannotRunDockerInDockerStopsBeforeRegistering()
    {
        await using var server = await new FakeOctopusServerBuilder().Build();

        var (exitCode, logs) = await new TentacleContainerBuilder(server)
            .WithEnvironmentVariable("DISABLE_DIND", "N")
            .RunUntilExit();

        exitCode.Should().NotBe(0).And.NotBe(TerminatedExitCode, "the exit code should be Docker-in-Docker's, not that of Tentacle being stopped");
        logs.Should().Contain("so Tentacle has not been configured or registered");
        logs.Should().Contain("Run the container with --privileged, or set DISABLE_DIND=Y");
        server.RequestLog.Should().BeEmpty("the container should stop before it contacts the server");
    }
}
