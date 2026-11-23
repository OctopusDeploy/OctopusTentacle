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

    readonly FakeOctopusServer server;
    TentacleCommunicationStyle communicationStyle = TentacleCommunicationStyle.Listening;
    string? workerPoolName;
    Dictionary<string, string> credentials = new() { ["ServerApiKey"] = ApiKey };
    readonly List<string> volumeMounts = new();

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

    public TentacleContainerBuilder WithVolume(DockerVolume volume, string containerPath)
    {
        volumeMounts.Add(volume.MountAt(containerPath));
        return this;
    }

    public async Task<TentacleContainer> Build()
    {
        using var cancellationTokenSource = new CancellationTokenSource(DockerCommandTimeout);
        var cancellationToken = cancellationTokenSource.Token;

        var machineName = $"tentacle-docker-test-{communicationStyle}-{Guid.NewGuid():N}"[..48].ToLowerInvariant();
        var image = TentacleImage.Resolve();
        TestContext.Out.WriteLine($"Testing {image} as a {communicationStyle} Tentacle named {machineName}");

        var registration = server.WaitForRegistration(FakeOctopusServerBuilder.SpaceName, machineName);

        var docker = await DockerContainer.Run(image, machineName, RunOptions(machineName), cancellationToken);
        var tentacle = new TentacleContainer(docker, server, machineName, communicationStyle);
        try
        {
            await tentacle.Start(registration, cancellationToken);
            return tentacle;
        }
        catch
        {
            await tentacle.DisposeAsync();
            throw;
        }
    }

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
            // Docker-in-Docker needs --privileged; the dind payload is covered by the image smoke tests.
            ["DISABLE_DIND"] = "Y",
            ["ServerUrl"] = $"http://{TentacleContainer.DockerHost}:{server.Port}",
            ["Space"] = FakeOctopusServerBuilder.SpaceName,
            ["TargetName"] = machineName
        };

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

        return environment;
    }
}
