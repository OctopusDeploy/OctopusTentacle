using System.Text;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.CommonTestUtils.Logging;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration.Setup;

public class DockerImageLoader
{
    readonly TemporaryDirectory temporaryDirectory;
    readonly ILogger logger;
    readonly string kindExePath;

    public DockerImageLoader(TemporaryDirectory temporaryDirectory, ILogger logger, string kindExePath)
    {
        this.temporaryDirectory = temporaryDirectory;
        this.logger = logger;
        this.kindExePath = kindExePath;
    }

    public async Task<string?> LoadMostRecentImageIntoKind(string clusterName)
    {
        var mostRecentTag = await FindMostRecentTag();

        return !string.IsNullOrWhiteSpace(mostRecentTag)
            ? await LoadImageIntoKind(mostRecentTag, clusterName)
            : null;
    }

    async Task<string?> FindMostRecentTag()
    {
        var sb = new StringBuilder();
        var tags = new List<string>();
        var sprLogger = new LoggerConfiguration()
            .WriteTo.Logger(logger)
            .WriteTo.StringBuilder(sb)
            .CreateLogger();

        var exitCode = await SilentProcessRunner.ExecuteCommandAsync(
            "docker",
            "images octopusdeploy/kubernetes-agent-tentacle --format \"{{.Tag}}\"",
            temporaryDirectory.DirectoryPath,
            sprLogger.Debug,
            line =>
            {
                sprLogger.Information(line);
                tags.Add(line);
            },
            sprLogger.Error,
            cancel: CancellationToken.None,
            abandon: CancellationToken.None
        );

        if (exitCode != 0)
        {
            logger.Error("Failed to get latest image tag from docker");
            throw new InvalidOperationException($"Failed to get latest image tag from docker. Logs: {sb}");
        }

        return tags.FirstOrDefault();
    }

    async Task<string> LoadImageIntoKind(string mostRecentTag, string clusterName)
    {
        var image = $"octopusdeploy/kubernetes-agent-tentacle:{mostRecentTag}";

        var sb = new StringBuilder();
        var sprLogger = new LoggerConfiguration()
            .WriteTo.Logger(logger)
            .WriteTo.StringBuilder(sb)
            .CreateLogger();

        var exitCode = await SilentProcessRunner.ExecuteCommandAsync(
            kindExePath,
            $"load docker-image {image} --name={clusterName}",
            temporaryDirectory.DirectoryPath,
            sprLogger.Debug,
            sprLogger.Information,
            sprLogger.Error,
            cancel: CancellationToken.None,
            abandon: CancellationToken.None
        );

        if (exitCode != 0)
        {
            logger.Error("Failed to load the Kubernetes Tentacle image into Kind");
            throw new InvalidOperationException($"Failed to load the Kubernetes Tentacle image into Kind. Logs: {sb}");
        }

        return image;
    }
}