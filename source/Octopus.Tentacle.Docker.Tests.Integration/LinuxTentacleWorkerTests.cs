using System.Text.RegularExpressions;
using FluentAssertions;
using Octopus.Tentacle.Docker.Tests.Integration.Setup;

namespace Octopus.Tentacle.Docker.Tests.Integration;

/// <summary>
/// Runs the image as a worker: TargetWorkerPool sends configure-tentacle.sh down its register-worker branch instead of
/// register-with, with its own validation and arguments. Once registered, a worker's agent is the same as a
/// deployment target's, so running scripts is left to LinuxTentacleImageTests.
/// </summary>
[TestFixture(TentacleCommunicationStyle.Listening)]
[TestFixture(TentacleCommunicationStyle.Polling)]
public class LinuxTentacleWorkerTests : TentacleContainerFixture
{
    // RegisterWorkerCommandBase opens at least this many polling connections for workers, where targets open one.
    const int MinimumWorkerPollingConnections = 5;

    public LinuxTentacleWorkerTests(TentacleCommunicationStyle communicationStyle) : base(communicationStyle, "Workers")
    {
    }

    protected override void ConfigureContainer(IDictionary<string, string> environment)
    {
        // configure-tentacle.sh rejects environments and roles alongside a worker pool.
        environment.Remove("TargetEnvironment");
        environment.Remove("TargetRole");
        // Has spaces, so this also checks the pool name survives the script's argument handling.
        environment["TargetWorkerPool"] = FakeOctopusServer.DefaultWorkerPoolName;
    }

    [Test]
    public async Task TentacleRegistersAsAWorkerInTheGivenPool()
    {
        RegisteredMachine.IsWorker.Should().BeTrue();
        RegisteredMachine.Name.Should().Be(MachineName);
        RegisteredMachine.WorkerPoolIds.Should().Equal(FakeOctopusServer.DefaultWorkerPoolId);
        RegisteredMachine.Roles.Should().BeEmpty();
        RegisteredMachine.EnvironmentIds.Should().BeEmpty();

        if (CommunicationStyle == TentacleCommunicationStyle.Listening)
        {
            RegisteredMachine.CommunicationStyle.Should().Be("TentaclePassive");
            RegisteredMachine.Uri.Should().Be($"https://{MachineName}:{TentacleContainer.ListeningPort}/");
        }
        else
        {
            RegisteredMachine.CommunicationStyle.Should().Be("TentacleActive");
            RegisteredMachine.Uri.Should().StartWith("poll://");

            // The agent logs how many connections it opens, which shows the worker default was saved and used.
            var logs = await Container.GetLogs();
            var match = Regex.Match(logs, @"Starting (\d+) polling connections");
            match.Success.Should().BeTrue("the agent logs how many polling connections it starts");
            int.Parse(match.Groups[1].Value).Should().BeGreaterThanOrEqualTo(MinimumWorkerPollingConnections);
        }
    }
}
