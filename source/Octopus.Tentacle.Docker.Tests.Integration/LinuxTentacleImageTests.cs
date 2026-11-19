using FluentAssertions;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Client.Scripts.Models.Builders;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.CommonTestUtils.Diagnostics;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Docker.Tests.Integration.Setup;
using Octopus.Tentacle.Tests.Integration.Common;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using NUnit.Framework.Interfaces;

namespace Octopus.Tentacle.Docker.Tests.Integration;

/// <summary>
/// Runs the Linux Tentacle image the way customers do, registers it with a fake Octopus Server, then runs scripts on
/// it over Halibut. Each fixture gets its own container and its own space on the shared server.
/// </summary>
[TestFixture(TentacleCommunicationStyle.Listening)]
[TestFixture(TentacleCommunicationStyle.Polling)]
public class LinuxTentacleImageTests
{
    const string EnvironmentName = "Development";
    const string Role = "docker-test";

    // Emulating an amd64 image on an arm64 machine makes start-up slow; be generous.
    static readonly TimeSpan RegistrationTimeout = TimeSpan.FromMinutes(5);
    static readonly TimeSpan ScriptTimeout = TimeSpan.FromMinutes(3);

    readonly TentacleCommunicationStyle communicationStyle;
    readonly string spaceName;
    readonly string machineName;

    TentacleContainer? container;
    RegisteredMachine? registeredMachine;
    TentacleClient? tentacleClient;

    public LinuxTentacleImageTests(TentacleCommunicationStyle communicationStyle)
    {
        this.communicationStyle = communicationStyle;
        spaceName = $"Docker-{communicationStyle}";
        machineName = $"tentacle-docker-test-{communicationStyle}-{Guid.NewGuid():N}"[..48].ToLowerInvariant();
    }

    TentacleContainer Container => container ?? throw new InvalidOperationException("The container has not been started");
    RegisteredMachine RegisteredMachine => registeredMachine ?? throw new InvalidOperationException("The Tentacle has not registered");
    TentacleClient TentacleClient => tentacleClient ?? throw new InvalidOperationException("The TentacleClient has not been built");

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        using var cancellationTokenSource = new CancellationTokenSource(RegistrationTimeout);
        var cancellationToken = cancellationTokenSource.Token;

        var image = TentacleImage.Resolve();
        TestContext.Progress.WriteLine($"Testing {image} as a {communicationStyle} Tentacle named {machineName} in space {spaceName}");

        var server = DockerTestsSetUpFixture.Server;
        server.AddSpace(spaceName, EnvironmentName);
        var registration = server.WaitForRegistration(spaceName, machineName);

        var environment = new Dictionary<string, string>
        {
            ["ACCEPT_EULA"] = "Y",
            // Docker-in-Docker needs --privileged; the dind payload is covered by the image smoke tests.
            ["DISABLE_DIND"] = "Y",
            ["ServerUrl"] = $"http://{TentacleContainer.DockerHost}:{server.Port}",
            ["ServerApiKey"] = "API-TENTACLEDOCKERTESTSNOTAREALKEY",
            ["Space"] = spaceName,
            ["TargetEnvironment"] = EnvironmentName,
            ["TargetRole"] = Role,
            ["TargetName"] = machineName
        };

        if (communicationStyle == TentacleCommunicationStyle.Polling)
        {
            environment["ServerPort"] = DockerTestsSetUpFixture.PollingPort.ToString();
        }

        try
        {
            container = await TentacleContainer.Start(image, machineName, communicationStyle, environment, cancellationToken);
            registeredMachine = await WaitForRegistration(registration, cancellationToken);
            tentacleClient = await BuildTentacleClient(cancellationToken);
        }
        catch
        {
            await WriteDiagnostics("Setting up the Tentacle container failed");
            throw;
        }
    }

    async Task<RegisteredMachine> WaitForRegistration(Task<RegisteredMachine> registration, CancellationToken cancellationToken)
    {
        while (!registration.IsCompleted)
        {
            // The entrypoint exits if configuration or registration fails, so there is no point waiting out the timeout.
            var exit = await Container.GetExitDescription(cancellationToken);
            if (exit is not null)
            {
                throw new InvalidOperationException($"The Tentacle container stopped before registering: {exit}");
            }

            await Task.WhenAny(registration, Task.Delay(TimeSpan.FromSeconds(2), cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
        }

        return await registration;
    }

    async Task<TentacleClient> BuildTentacleClient(CancellationToken cancellationToken)
    {
        // Listening Tentacles registered the address the server should use, but the tests are on the host and reach
        // the container through its published port instead.
        var uri = communicationStyle == TentacleCommunicationStyle.Listening
            ? new Uri($"https://localhost:{await Container.GetPublishedListeningPort(cancellationToken)}/")
            : new Uri(RegisteredMachine.Uri!);

        return TestServerHalibut.BuildTentacleClient(uri, RegisteredMachine.Thumbprint, DockerTestsSetUpFixture.Halibut, _ => { });
    }

    [TearDown]
    public async Task TearDown()
    {
        if (TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed)
        {
            await WriteDiagnostics($"{TestContext.CurrentContext.Test.Name} failed");
        }
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    [Test]
    public void TentacleRegistersItselfFromTheEnvironmentVariables()
    {
        RegisteredMachine.Name.Should().Be(machineName);
        RegisteredMachine.Roles.Should().Equal(Role);
        RegisteredMachine.EnvironmentIds.Should().HaveCount(1);
        RegisteredMachine.Thumbprint.Should().NotBeNullOrWhiteSpace();

        if (communicationStyle == TentacleCommunicationStyle.Listening)
        {
            RegisteredMachine.CommunicationStyle.Should().Be("TentaclePassive");
            // The image defaults PublicHostNameConfiguration to ComputerName, and the container's hostname is its name.
            RegisteredMachine.Uri.Should().Be($"https://{machineName}:{TentacleContainer.ListeningPort}/");
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

    [Test]
    public async Task ScriptFailuresAreReportedWithTheirExitCode()
    {
        var command = new ExecuteShellScriptCommandBuilder(LoggingUtils.CurrentTestHash(), ScriptIsolationLevel.NoIsolation)
            .WithScriptBody("echo 'about to fail'\nexit 3\n")
            .Build();

        var (result, output) = await ExecuteScript(command);

        result.State.Should().Be(ProcessState.Complete);
        result.ExitCode.Should().Be(3);
        output.Should().Contain("about to fail");
    }

    async Task<(ScriptExecutionResult Result, string Output)> ExecuteScript(ExecuteScriptCommand command)
    {
        using var cancellationTokenSource = new CancellationTokenSource(ScriptTimeout);
        var logs = new List<ProcessOutput>();

        var result = await TentacleClient.ExecuteScript(
            command,
            status => logs.AddRange(status.Logs),
            _ => Task.CompletedTask,
            new InMemoryLog(),
            cancellationTokenSource.Token);

        var output = string.Join(Environment.NewLine, logs.Select(l => $"[{l.Source}] {l.Text}"));
        TestContext.Out.WriteLine("Script output:");
        TestContext.Out.WriteLine(output);

        return (result, output);
    }

    async Task WriteDiagnostics(string reason)
    {
        var writer = TestContext.Progress;
        writer.WriteLine($"==== {reason}: diagnostics for {communicationStyle} Tentacle {machineName} ====");

        if (container is not null)
        {
            writer.WriteLine($"---- docker logs {container.Name} ----");
            writer.WriteLine(await container.GetLogs());
        }

        writer.WriteLine("---- fake Octopus Server requests ----");
        foreach (var request in DockerTestsSetUpFixture.Server.RequestLog)
        {
            writer.WriteLine(request);
        }

        foreach (var request in DockerTestsSetUpFixture.Server.UnhandledRequests)
        {
            writer.WriteLine($"UNHANDLED: {request}");
        }

        if (registeredMachine is not null)
        {
            writer.WriteLine("---- registered machine ----");
            writer.WriteLine(registeredMachine.Json);
        }

        writer.WriteLine("==== end of diagnostics ====");
    }
}
