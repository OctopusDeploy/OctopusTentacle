using System.Text.RegularExpressions;
using FluentAssertions;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Client.Scripts.Models.Builders;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.CommonTestUtils.Diagnostics;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Tests.Integration.Common;
using Octopus.Tentacle.Tests.Integration.Common.FakeOctopus;

namespace Octopus.Tentacle.Docker.Tests.Integration.Setup;

public enum TentacleCommunicationStyle
{
    Listening,
    Polling
}

/// <summary>
/// A container running the Linux Tentacle image exactly as customers run it, registered with a fake Octopus Server,
/// plus a TentacleClient to talk to it over Halibut. Built with <see cref="TentacleContainerBuilder"/>.
///
/// Disposing it writes its logs and what it registered to the test's output, then removes the container.
/// </summary>
public sealed class TentacleContainer : IAsyncDisposable
{
    /// <summary>The port Tentacle listens on inside the container.</summary>
    public const int ListeningPort = 10933;

    /// <summary>How Tentacle containers address the machine running the tests.</summary>
    public const string DockerHost = "host.docker.internal";

    /// <summary>The secrets the image's entrypoint uses to register and configure Tentacle, which it must not pass on to the agent.</summary>
    static readonly string[] RegistrationCredentialVariables = { "ServerApiKey", "BearerToken", "ServerUsername", "ServerPassword", "TentacleCertificateBase64" };

    // Emulating an amd64 image on an arm64 machine makes start-up slow; be generous.
    static readonly TimeSpan RegistrationTimeout = TimeSpan.FromMinutes(5);
    static readonly TimeSpan ScriptTimeout = TimeSpan.FromMinutes(3);
    static readonly TimeSpan DockerCommandTimeout = TimeSpan.FromMinutes(2);

    // How often to check the container is still running while waiting on it.
    static readonly TimeSpan ContainerCheckInterval = TimeSpan.FromSeconds(2);

    /// <summary>What configure-tentacle.sh prints when it starts without registering, so the Tentacle can be added by hand.</summary>
    static readonly Regex ManualRegistrationThumbprint = new("Add it in Octopus as a listening Tentacle with thumbprint (?<thumbprint>[0-9A-Fa-f]{40})");

    readonly FakeOctopusServer server;
    RegisteredMachine? registeredMachine;
    string? unregisteredThumbprint;
    TentacleClient? tentacleClient;

    internal TentacleContainer(DockerContainer docker, FakeOctopusServer server, string machineName, TentacleCommunicationStyle communicationStyle)
    {
        Docker = docker;
        this.server = server;
        MachineName = machineName;
        CommunicationStyle = communicationStyle;
    }

    public DockerContainer Docker { get; }
    public string MachineName { get; }
    public TentacleCommunicationStyle CommunicationStyle { get; }

    public RegisteredMachine RegisteredMachine => registeredMachine ?? throw new InvalidOperationException("The Tentacle has not registered");
    public TentacleClient TentacleClient => tentacleClient ?? throw new InvalidOperationException("The TentacleClient has not been built");

    /// <summary>The Tentacle's thumbprint: the one it registered with, or the one it printed if it did not register.</summary>
    public string Thumbprint => registeredMachine?.Thumbprint ?? unregisteredThumbprint ?? throw new InvalidOperationException("The Tentacle has not started");

    internal async Task Start(Task<RegisteredMachine> registration, CancellationToken cancellationToken)
    {
        registeredMachine = await WaitForRegistration(registration);
        // The agent starts after registration; if it dies straight away, say so rather than fail on a missing port.
        await ThrowIfStopped("after registering");
        tentacleClient = await BuildTentacleClient(cancellationToken);
    }

    /// <summary>
    /// Waits for a Tentacle started with ServerThumbprint to print its thumbprint, which stands in for registering: it
    /// is what a user would add to Octopus by hand.
    /// </summary>
    internal async Task StartUnregistered(CancellationToken cancellationToken)
    {
        var match = await WaitForLogMatch(ManualRegistrationThumbprint, RegistrationTimeout);
        unregisteredThumbprint = match.Groups["thumbprint"].Value;
        // The agent starts after configuration; if it dies straight away, say so rather than fail on a missing port.
        await ThrowIfStopped("after configuring");
        tentacleClient = await BuildTentacleClient(cancellationToken);
    }

    async Task<RegisteredMachine> WaitForRegistration(Task<RegisteredMachine> registration)
    {
        var timeout = Task.Delay(RegistrationTimeout);
        while (!registration.IsCompleted)
        {
            // The entrypoint exits if configuration or registration fails, so there is no point waiting out the timeout.
            // Registration can complete while the container is checked; then the caller reports it as stopping after.
            var exit = await Docker.GetExitDescription(CancellationToken.None);
            if (exit is not null && !registration.IsCompleted)
            {
                throw new InvalidOperationException($"The Tentacle container stopped before registering: {exit}");
            }

            if (await Task.WhenAny(registration, timeout, Task.Delay(ContainerCheckInterval)) == timeout && !registration.IsCompleted)
            {
                throw new TimeoutException(
                    $"The Tentacle container did not register within {RegistrationTimeout.TotalSeconds:0} seconds. " +
                    $"It is {await Docker.DescribeState()}; see its logs below for where it got to.");
            }
        }

        return await registration;
    }

    async Task<TentacleClient> BuildTentacleClient(CancellationToken cancellationToken)
    {
        // Listening Tentacles registered the address the server should use, but the tests are on the host and reach
        // the container through its published port instead.
        var uri = CommunicationStyle == TentacleCommunicationStyle.Listening
            ? new Uri($"https://localhost:{await Docker.GetPublishedPort(ListeningPort, cancellationToken)}/")
            : new Uri(RegisteredMachine.Uri!);

        return TestServerHalibut.BuildTentacleClient(uri, Thumbprint, server.Halibut, _ => { });
    }

    /// <summary>
    /// Restarts the container as `docker restart` does, then points the TentacleClient at it again: Docker may publish
    /// the listening port on a different host port after a restart.
    /// </summary>
    public async Task Restart(TimeSpan gracePeriod)
    {
        using var cancellationTokenSource = new CancellationTokenSource(DockerCommandTimeout + gracePeriod);
        await Docker.Restart(gracePeriod, cancellationTokenSource.Token);
        tentacleClient = await BuildTentacleClient(cancellationTokenSource.Token);
    }

    /// <summary>
    /// Waits for the container to log something. Registration completes before the agent starts, so anything the
    /// agent logs may not be there yet when a test begins.
    /// </summary>
    public async Task<Match> WaitForLogMatch(Regex pattern, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (true)
        {
            var match = pattern.Match(await Docker.GetLogs());
            if (match.Success)
            {
                return match;
            }

            await ThrowIfStopped($"while waiting for it to log '{pattern}'");
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException($"The Tentacle container did not log '{pattern}' within {timeout.TotalSeconds:0} seconds.");
            }

            await Task.Delay(ContainerCheckInterval);
        }
    }

    async Task ThrowIfStopped(string when)
    {
        var exit = await Docker.GetExitDescription(CancellationToken.None);
        if (exit is not null)
        {
            throw new InvalidOperationException($"The Tentacle container stopped {when}: {exit}");
        }
    }

    public async Task<(ScriptExecutionResult Result, string Output)> ExecuteScript(ExecuteScriptCommand command)
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
    /// cannot read them. Checks all of them, whichever the Tentacle registered with.
    /// </summary>
    public async Task AssertRegistrationCredentialsAreNotVisibleToScripts()
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

            var exit = await Docker.GetExitDescription(CancellationToken.None);
            if (exit is not null)
            {
                return exit;
            }
        }
    }

    /// <summary>
    /// Whether the test passed or not is unknown here (a failed assertion is still on its way to NUnit), so this always
    /// writes what is needed to work out what went wrong. It goes to the test's own output, not the console.
    /// </summary>
    async Task WriteDiagnostics()
    {
        var writer = TestContext.Out;
        writer.WriteLine($"==== diagnostics for {CommunicationStyle} Tentacle {MachineName} ====");
        writer.WriteLine($"---- container {Docker.Name}: {await Docker.DescribeState()} ----");
        writer.WriteLine($"---- docker logs {Docker.Name} ----");
        writer.WriteLine(await Docker.GetLogs());

        if (registeredMachine is not null)
        {
            writer.WriteLine("---- registered machine ----");
            writer.WriteLine(registeredMachine.Json);
        }

        writer.WriteLine("==== end of diagnostics ====");
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await WriteDiagnostics();
        }
        catch (Exception e)
        {
            TestContext.Out.WriteLine($"Could not write diagnostics for container {Docker.Name}: {e.Message}");
        }

        await Docker.DisposeAsync();
    }
}
