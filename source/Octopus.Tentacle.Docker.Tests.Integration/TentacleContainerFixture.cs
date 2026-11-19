using FluentAssertions;
using NUnit.Framework.Interfaces;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Client.Scripts.Models.Builders;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.CommonTestUtils.Diagnostics;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Docker.Tests.Integration.Setup;
using Octopus.Tentacle.Tests.Integration.Common;

namespace Octopus.Tentacle.Docker.Tests.Integration;

/// <summary>
/// Starts the Linux Tentacle image the way customers do, waits for it to register with the fake Octopus Server, and
/// builds a TentacleClient to talk to it over Halibut. Each fixture gets its own container and its own space on the
/// shared server.
/// </summary>
public abstract class TentacleContainerFixture
{
    protected const string EnvironmentName = "Development";
    protected const string Role = "docker-test";
    protected const string ApiKey = "API-TENTACLEDOCKERTESTSNOTAREALKEY";

    /// <summary>The credentials the image's entrypoint uses to register, which it must not pass on to the agent.</summary>
    static readonly string[] RegistrationCredentialVariables = { "ServerApiKey", "BearerToken", "ServerUsername", "ServerPassword" };

    // Emulating an amd64 image on an arm64 machine makes start-up slow; be generous.
    static readonly TimeSpan RegistrationTimeout = TimeSpan.FromMinutes(5);
    static readonly TimeSpan ScriptTimeout = TimeSpan.FromMinutes(3);

    // Docker commands, including pulling the image, get longer than registration does.
    static readonly TimeSpan DockerCommandTimeout = TimeSpan.FromMinutes(10);

    // How often to check the container is still running while waiting on it.
    static readonly TimeSpan ContainerCheckInterval = TimeSpan.FromSeconds(2);

    DateTimeOffset fixtureStarted;
    TentacleContainer? container;
    RegisteredMachine? registeredMachine;
    TentacleClient? tentacleClient;

    protected TentacleContainerFixture(TentacleCommunicationStyle communicationStyle, string spacePrefix)
    {
        CommunicationStyle = communicationStyle;
        SpaceName = $"{spacePrefix}-{communicationStyle}";
        MachineName = $"tentacle-docker-test-{communicationStyle}-{Guid.NewGuid():N}"[..48].ToLowerInvariant();
    }

    protected TentacleCommunicationStyle CommunicationStyle { get; }
    protected string SpaceName { get; }
    protected string MachineName { get; }

    protected TentacleContainer Container => container ?? throw new InvalidOperationException("The container has not been started");
    protected RegisteredMachine RegisteredMachine => registeredMachine ?? throw new InvalidOperationException("The Tentacle has not registered");
    protected TentacleClient TentacleClient => tentacleClient ?? throw new InvalidOperationException("The TentacleClient has not been built");

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        fixtureStarted = DateTimeOffset.UtcNow;
        using var cancellationTokenSource = new CancellationTokenSource(DockerCommandTimeout);
        var cancellationToken = cancellationTokenSource.Token;

        var image = TentacleImage.Resolve();
        TestContext.Progress.WriteLine($"Testing {image} as a {CommunicationStyle} Tentacle named {MachineName} in space {SpaceName}");

        var server = DockerTestsSetUpFixture.Server;
        server.AddSpace(SpaceName, EnvironmentName);
        var registration = server.WaitForRegistration(SpaceName, MachineName);

        var environment = new Dictionary<string, string>
        {
            ["ACCEPT_EULA"] = "Y",
            // Docker-in-Docker needs --privileged; the dind payload is covered by the image smoke tests.
            ["DISABLE_DIND"] = "Y",
            ["ServerUrl"] = $"http://{TentacleContainer.DockerHost}:{server.Port}",
            ["ServerApiKey"] = ApiKey,
            ["Space"] = SpaceName,
            ["TargetEnvironment"] = EnvironmentName,
            ["TargetRole"] = Role,
            ["TargetName"] = MachineName
        };

        if (CommunicationStyle == TentacleCommunicationStyle.Polling)
        {
            environment["ServerPort"] = DockerTestsSetUpFixture.PollingPort.ToString();
        }

        ConfigureContainer(environment);

        try
        {
            container = await TentacleContainer.Start(image, MachineName, CommunicationStyle, environment, cancellationToken);
            registeredMachine = await WaitForRegistration(registration);
            // The agent starts after registration; if it dies straight away, say so rather than fail on a missing port.
            await ThrowIfContainerStopped("after registering");
            tentacleClient = await BuildTentacleClient(cancellationToken);
        }
        catch
        {
            await WriteDiagnostics("Setting up the Tentacle container failed");
            throw;
        }
    }

    /// <summary>
    /// Change the environment variables the container is started with, e.g. to register as a worker or authenticate
    /// differently. By default it registers as a deployment target with an API key.
    /// </summary>
    protected virtual void ConfigureContainer(IDictionary<string, string> environment)
    {
    }

    async Task<RegisteredMachine> WaitForRegistration(Task<RegisteredMachine> registration)
    {
        var timeout = Task.Delay(RegistrationTimeout);
        while (!registration.IsCompleted)
        {
            // The entrypoint exits if configuration or registration fails, so there is no point waiting out the timeout.
            // Registration can complete while the container is checked; then the caller reports it as stopping after.
            var exit = await Container.GetExitDescription(CancellationToken.None);
            if (exit is not null && !registration.IsCompleted)
            {
                throw new InvalidOperationException($"The Tentacle container stopped before registering: {exit}");
            }

            if (await Task.WhenAny(registration, timeout, Task.Delay(ContainerCheckInterval)) == timeout && !registration.IsCompleted)
            {
                throw new TimeoutException(
                    $"The Tentacle container did not register within {RegistrationTimeout.TotalSeconds:0} seconds. " +
                    $"It is {await Container.DescribeState()}; see its logs below for where it got to.");
            }
        }

        return await registration;
    }

    protected async Task ThrowIfContainerStopped(string when)
    {
        var exit = await Container.GetExitDescription(CancellationToken.None);
        if (exit is not null)
        {
            throw new InvalidOperationException($"The Tentacle container stopped {when}: {exit}");
        }
    }

    async Task<TentacleClient> BuildTentacleClient(CancellationToken cancellationToken)
    {
        // Listening Tentacles registered the address the server should use, but the tests are on the host and reach
        // the container through its published port instead.
        var uri = CommunicationStyle == TentacleCommunicationStyle.Listening
            ? new Uri($"https://localhost:{await Container.GetPublishedListeningPort(cancellationToken)}/")
            : new Uri(RegisteredMachine.Uri!);

        return TestServerHalibut.BuildTentacleClient(uri, RegisteredMachine.Thumbprint, DockerTestsSetUpFixture.Halibut, _ => { });
    }

    /// <summary>Docker may publish the listening port on a different host port after a restart, so look it up again.</summary>
    protected async Task RebuildTentacleClient()
    {
        using var cancellationTokenSource = new CancellationTokenSource(DockerCommandTimeout);
        tentacleClient = await BuildTentacleClient(cancellationTokenSource.Token);
    }

    /// <summary>
    /// If the agent died after registering, every test would otherwise spend minutes in Halibut retries and then fail
    /// with a connection error. Fail straight away with how the container stopped instead.
    /// </summary>
    [SetUp]
    public async Task SetUp()
    {
        await ThrowIfContainerStopped("after registering");
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

    protected async Task<(ScriptExecutionResult Result, string Output)> ExecuteScript(ExecuteScriptCommand command)
    {
        using var cancellationTokenSource = new CancellationTokenSource(ScriptTimeout);
        using var stopWatching = new CancellationTokenSource();
        var logs = new List<ProcessOutput>();

        var script = TentacleClient.ExecuteScript(
            command,
            status => logs.AddRange(status.Logs),
            _ => Task.CompletedTask,
            new InMemoryLog(),
            cancellationTokenSource.Token);

        // If the container dies mid-script, Halibut would keep retrying until the script times out. Watch it instead,
        // and stop waiting on the script as soon as it is gone.
        var containerStopped = WaitForContainerToStop(stopWatching.Token);
        if (await Task.WhenAny(script, containerStopped) == containerStopped && await containerStopped is { } exit)
        {
            cancellationTokenSource.Cancel();
            // Nothing will await the abandoned script, so observe its failure here.
            _ = script.ContinueWith(t => t.Exception, TaskScheduler.Default);
            throw new InvalidOperationException($"The Tentacle container stopped while running a script: {exit}");
        }

        stopWatching.Cancel();
        var result = await script;

        var output = string.Join(Environment.NewLine, logs.Select(l => $"[{l.Source}] {l.Text}"));
        TestContext.Out.WriteLine("Script output:");
        TestContext.Out.WriteLine(output);

        return (result, output);
    }

    /// <summary>
    /// configure-and-run.sh unsets the registration credentials before starting the agent, so deployment scripts
    /// cannot read them. Checks all of them, whichever the fixture registered with.
    /// </summary>
    protected async Task AssertRegistrationCredentialsAreNotVisibleToScripts()
    {
        var checks = string.Join("\n", RegistrationCredentialVariables.Select(v => $"if [ -n \"${{{v}+set}}\" ]; then echo \"VISIBLE: {v}\"; fi"));
        var command = new ExecuteShellScriptCommandBuilder(LoggingUtils.CurrentTestHash(), ScriptIsolationLevel.NoIsolation)
            .WithScriptBody(checks + "\necho 'credential check complete'\n")
            .Build();

        var (result, output) = await ExecuteScript(command);

        result.ExitCode.Should().Be(0);
        // Guards against a script that silently did nothing.
        output.Should().Contain("credential check complete");
        output.Should().NotContain("VISIBLE:");
    }

    /// <summary>Completes with how the container stopped, or with null once <paramref name="stopWatching"/> is cancelled.</summary>
    async Task<string?> WaitForContainerToStop(CancellationToken stopWatching)
    {
        while (true)
        {
            try
            {
                await Task.Delay(ContainerCheckInterval, stopWatching);
            }
            catch (OperationCanceledException)
            {
                return null;
            }

            var exit = await Container.GetExitDescription(CancellationToken.None);
            if (exit is not null)
            {
                return exit;
            }
        }
    }

    async Task WriteDiagnostics(string reason)
    {
        var writer = TestContext.Progress;
        writer.WriteLine($"==== {reason}: diagnostics for {CommunicationStyle} Tentacle {MachineName} ====");

        if (container is not null)
        {
            writer.WriteLine($"---- container {container.Name}: {await container.DescribeState()} ----");
            writer.WriteLine($"---- docker logs {container.Name} ----");
            writer.WriteLine(await container.GetLogs());
        }

        writer.WriteLine($"---- fake Octopus Server requests for {SpaceName} ----");
        foreach (var request in DockerTestsSetUpFixture.Server.RequestLogFor(SpaceName, fixtureStarted))
        {
            writer.WriteLine(request);
        }

        if (registeredMachine is not null)
        {
            writer.WriteLine("---- registered machine ----");
            writer.WriteLine(registeredMachine.Json);
        }

        writer.WriteLine("==== end of diagnostics ====");
    }
}
