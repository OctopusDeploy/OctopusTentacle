using System.Text.RegularExpressions;
using FluentAssertions;
using Octopus.Tentacle.Docker.Tests.Integration.Setup;

namespace Octopus.Tentacle.Docker.Tests.Integration;

/// <summary>
/// Runs the image as a worker: TargetWorkerPool sends configure-tentacle.sh down its register-worker branch instead of
/// register-with, with its own validation and arguments. Once registered, a worker's agent is the same as a
/// deployment target's, so running scripts is left to LinuxTentacleImageTests.
/// </summary>
public class LinuxTentacleWorkerTests
{
    // RegisterWorkerCommandBase opens at least this many polling connections for workers, where targets open one.
    const int MinimumWorkerPollingConnections = 5;

    // The agent logs this once it starts; the test can begin before then, as registration happens first.
    static readonly Regex PollingConnectionsStarted = new(@"Starting (\d+) polling connections");
    static readonly TimeSpan AgentStartTimeout = TimeSpan.FromSeconds(60);

    [Test]
    [TestCase(TentacleCommunicationStyle.Listening)]
    [TestCase(TentacleCommunicationStyle.Polling)]
    public async Task TentacleRegistersAsAWorkerInTheGivenPool(TentacleCommunicationStyle communicationStyle)
    {
        await using var server = await new FakeOctopusServerBuilder().Build();
        await using var tentacle = await new TentacleContainerBuilder(server)
            .WithCommunicationStyle(communicationStyle)
            // Has spaces, so this also checks the pool name survives the script's argument handling.
            .AsWorker(FakeOctopusServer.DefaultWorkerPoolName)
            .Build();

        var worker = tentacle.RegisteredMachine;
        worker.IsWorker.Should().BeTrue();
        worker.Name.Should().Be(tentacle.MachineName);
        worker.WorkerPoolIds.Should().Equal(FakeOctopusServer.DefaultWorkerPoolId);
        worker.Roles.Should().BeEmpty();
        worker.EnvironmentIds.Should().BeEmpty();

        if (communicationStyle == TentacleCommunicationStyle.Listening)
        {
            worker.CommunicationStyle.Should().Be("TentaclePassive");
            worker.Uri.Should().Be($"https://{tentacle.MachineName}:{TentacleContainer.ListeningPort}/");
        }
        else
        {
            worker.CommunicationStyle.Should().Be("TentacleActive");
            worker.Uri.Should().StartWith("poll://");

            // The agent logs how many connections it opens, which shows the worker default was saved and used.
            var match = await tentacle.WaitForLogMatch(PollingConnectionsStarted, AgentStartTimeout);
            int.Parse(match.Groups[1].Value).Should().BeGreaterThanOrEqualTo(MinimumWorkerPollingConnections);
        }
    }
}
