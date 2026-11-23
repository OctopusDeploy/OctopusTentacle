using Octopus.Tentacle.Tests.Integration.Common.FakeOctopus;

namespace Octopus.Tentacle.Docker.Tests.Integration.Setup;

/// <summary>
/// Starts the Linux Tentacle image the way customers do and waits for it to register with a fake Octopus Server:
/// <code>
/// await using var server = await new FakeOctopusServerBuilder().Build();
/// await using var tentacle = await new TentacleContainerBuilder(server).WithCommunicationStyle(TentacleCommunicationStyle.Polling).Build();
/// </code>
/// By default it registers as a listening deployment target, authenticating with <see cref="ApiKey"/>.
/// </summary>
public class TentacleContainerBuilder
{
    public const string Role = "docker-test";
    public const string ApiKey = "API-TENTACLEDOCKERTESTSNOTAREALKEY";

    // Docker commands, including pulling the image, take longer than registration does.
    static readonly TimeSpan DockerCommandTimeout = TimeSpan.FromMinutes(10);

    // How often to check whether a container expected to exit has.
    static readonly TimeSpan ContainerCheckInterval = TimeSpan.FromSeconds(2);

    readonly FakeOctopusServer server;
    TentacleCommunicationStyle communicationStyle = TentacleCommunicationStyle.Listening;
    string? workerPoolName;
    Dictionary<string, string> credentials = new() { ["ServerApiKey"] = ApiKey };
    string? serverThumbprint;
    string? certificateBase64;
    readonly Dictionary<string, string> extraEnvironment = new();
    readonly List<string> volumeMounts = new();
    RegisteredMachine? alreadyRegistered;

    public TentacleContainerBuilder(FakeOctopusServer server)
    {
        this.server = server;
    }

    public TentacleContainerBuilder WithCommunicationStyle(TentacleCommunicationStyle communicationStyle)
    {
        this.communicationStyle = communicationStyle;
        return this;
    }

    /// <summary>Registers as a worker in the given pool rather than as a deployment target.</summary>
    public TentacleContainerBuilder AsWorker(string workerPoolName)
    {
        this.workerPoolName = workerPoolName;
        return this;
    }

    public TentacleContainerBuilder WithBearerToken(string bearerToken)
    {
        credentials = new Dictionary<string, string> { ["BearerToken"] = bearerToken };
        return this;
    }

    public TentacleContainerBuilder WithUsernameAndPassword(string username, string password)
    {
        credentials = new Dictionary<string, string> { ["ServerUsername"] = username, ["ServerPassword"] = password };
        return this;
    }

    /// <summary>
    /// Starts the Tentacle with ServerThumbprint, so it trusts that server and does not register with it. It still
    /// listens; the fake server's HalibutRuntime connects to it as Octopus Server would once it is added by hand.
    /// </summary>
    public TentacleContainerBuilder WithoutRegistration(string serverThumbprint)
    {
        this.serverThumbprint = serverThumbprint;
        return this;
    }

    /// <summary>Imports this certificate with TentacleCertificateBase64 rather than letting Tentacle create one.</summary>
    public TentacleContainerBuilder WithCertificate(string certificateBase64)
    {
        this.certificateBase64 = certificateBase64;
        return this;
    }

    /// <summary>Sets an environment variable after the builder's own, so it can add to or override them.</summary>
    public TentacleContainerBuilder WithEnvironmentVariable(string name, string value)
    {
        extraEnvironment[name] = value;
        return this;
    }

    public TentacleContainerBuilder WithVolume(DockerVolume volume, string containerPath)
    {
        volumeMounts.Add(volume.MountAt(containerPath));
        return this;
    }

    /// <summary>
    /// Starts on volumes a previous container configured, so it skips registration. The TentacleClient trusts the
    /// previous registration's thumbprint.
    /// </summary>
    public TentacleContainerBuilder WithStartingAlreadyConfiguredTentacle(RegisteredMachine registeredMachine)
    {
        alreadyRegistered = registeredMachine;
        return this;
    }

    public async Task<TentacleContainer> Build()
    {
        if (serverThumbprint is not null && communicationStyle == TentacleCommunicationStyle.Polling)
        {
            throw new InvalidOperationException("A Tentacle that does not register can only listen");
        }

        using var cancellationTokenSource = new CancellationTokenSource(DockerCommandTimeout);
        var cancellationToken = cancellationTokenSource.Token;

        var machineName = NewMachineName();
        var image = TentacleImage.Resolve();
        TestContext.Out.WriteLine($"Testing {image} as a {communicationStyle} Tentacle named {machineName}{(serverThumbprint is null ? "" : " that does not register")}");

        var registration = serverThumbprint is not null ? null
            : alreadyRegistered is not null ? Task.FromResult(alreadyRegistered)
            : server.WaitForRegistration(FakeOctopusServerBuilder.SpaceName, machineName);

        var docker = await DockerContainer.Run(image, machineName, RunOptions(machineName), cancellationToken);
        var tentacle = new TentacleContainer(docker, server, machineName, communicationStyle);
        try
        {
            if (registration is null)
            {
                await tentacle.StartUnregistered(cancellationToken);
            }
            else
            {
                await tentacle.Start(registration, cancellationToken);
            }

            return tentacle;
        }
        catch
        {
            await tentacle.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Starts a container expected to fail its configuration, and returns its exit code and logs once it has stopped.
    /// The container is removed before returning.
    /// </summary>
    public async Task<(int ExitCode, string Logs)> RunUntilExit()
    {
        using var cancellationTokenSource = new CancellationTokenSource(DockerCommandTimeout);
        var cancellationToken = cancellationTokenSource.Token;

        var machineName = NewMachineName();
        var image = TentacleImage.Resolve();
        TestContext.Out.WriteLine($"Running {image} as {machineName}, expecting it to exit");

        await using var docker = await DockerContainer.Run(image, machineName, RunOptions(machineName), cancellationToken);
        while (await docker.GetExitDescription(cancellationToken) is null)
        {
            await Task.Delay(ContainerCheckInterval, cancellationToken);
        }

        var logs = await docker.GetLogs();
        TestContext.Out.WriteLine($"---- docker logs {docker.Name} ----");
        TestContext.Out.WriteLine(logs);

        return (await docker.GetExitCode(cancellationToken), logs);
    }

    string NewMachineName() => $"tentacle-docker-test-{communicationStyle}-{Guid.NewGuid():N}"[..48].ToLowerInvariant();

    IEnumerable<string> RunOptions(string machineName)
    {
        var options = new List<string>
        {
            "--hostname", machineName,
            // Docker Desktop provides host.docker.internal; this makes Linux engines provide it too.
            "--add-host", $"{TentacleContainer.DockerHost}:host-gateway"
        };

        foreach (var mount in volumeMounts)
        {
            options.AddRange(new[] { "--volume", mount });
        }

        if (communicationStyle == TentacleCommunicationStyle.Listening)
        {
            // Publish to a random loopback port: the tests connect to Tentacle from the host.
            options.AddRange(new[] { "--publish", $"127.0.0.1::{TentacleContainer.ListeningPort}" });
        }

        foreach (var (key, value) in EnvironmentVariables(machineName))
        {
            options.AddRange(new[] { "--env", $"{key}={value}" });
        }

        return options;
    }

    Dictionary<string, string> EnvironmentVariables(string machineName)
    {
        var environment = new Dictionary<string, string>
        {
            ["ACCEPT_EULA"] = "Y",
            // Docker-in-Docker needs --privileged; the dind payload is covered by the image smoke tests, and
            // LinuxTentacleDockerInDockerTests covers it failing to start.
            ["DISABLE_DIND"] = "Y",
            ["Space"] = FakeOctopusServerBuilder.SpaceName
        };

        if (serverThumbprint is not null)
        {
            // configure-tentacle.sh rejects registration and polling settings alongside ServerThumbprint.
            environment["ServerThumbprint"] = serverThumbprint;
        }
        else
        {
            environment["ServerUrl"] = $"http://{TentacleContainer.DockerHost}:{server.Port}";
            environment["TargetName"] = machineName;

            foreach (var (key, value) in credentials)
            {
                environment[key] = value;
            }

            if (workerPoolName is null)
            {
                environment["TargetEnvironment"] = FakeOctopusServerBuilder.EnvironmentName;
                environment["TargetRole"] = Role;
            }
            else
            {
                // configure-tentacle.sh rejects environments and roles alongside a worker pool.
                environment["TargetWorkerPool"] = workerPoolName;
            }

            if (communicationStyle == TentacleCommunicationStyle.Polling)
            {
                environment["ServerPort"] = server.PollingPort.ToString();
            }
        }

        if (certificateBase64 is not null)
        {
            environment["TentacleCertificateBase64"] = certificateBase64;
        }

        foreach (var (key, value) in extraEnvironment)
        {
            environment[key] = value;
        }

        return environment;
    }
}
