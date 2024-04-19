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

    public string? LoadMostRecentImageIntoKind(string clusterName)
    {
        var mostRecentTag = FindMostRecentTag();

        return !string.IsNullOrWhiteSpace(mostRecentTag)
            ? LoadImageIntoKind(mostRecentTag, clusterName)
            : null;
    }

    string? FindMostRecentTag()
    {
        var sb = new StringBuilder();
        var tags = new List<string>();
        var sprLogger = new LoggerConfiguration()
            .WriteTo.Logger(logger)
            .WriteTo.StringBuilder(sb)
            .CreateLogger();

        var exitCode = SilentProcessRunner.ExecuteCommand(
            "docker",
            "images octopusdeploy/kubernetes-tentacle --format \"{{.Tag}}\"",
            temporaryDirectory.DirectoryPath,
            sprLogger.Debug,
            line =>
            {
                sprLogger.Information(line);
                tags.Add(line);
            },
            sprLogger.Error,
            CancellationToken.None
        );

        if (exitCode != 0)
        {
            logger.Error("Failed to get latest image tag from docker");
            throw new InvalidOperationException($"Failed to get latest image tag from docker. Logs: {sb}");
        }
        
        return tags.FirstOrDefault();
    }

    string LoadImageIntoKind(string mostRecentTag, string clusterName)
    {
        var image = $"octopusdeploy/kubernetes-tentacle:{mostRecentTag}";

        var sb = new StringBuilder();
        var sprLogger = new LoggerConfiguration()
            .WriteTo.Logger(logger)
            .WriteTo.StringBuilder(sb)
            .CreateLogger();

        var exitCode = SilentProcessRunner.ExecuteCommand(
            kindExePath,
            $"load docker-image {image} --name={clusterName}",
            temporaryDirectory.DirectoryPath,
            sprLogger.Debug,
            sprLogger.Information,
            sprLogger.Error,
            CancellationToken.None
        );

        if (exitCode != 0)
        {
            logger.Error("Failed to load the Kubernetes Tentacle image into Kind");
            throw new InvalidOperationException($"Failed to load the Kubernetes Tentacle image into Kind. Logs: {sb}");
        }

        return image;
    }
}