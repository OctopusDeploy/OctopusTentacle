using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Docker.Tests.Integration.Setup;

/// <summary>Works out which Linux Tentacle image to test.</summary>
public static class TentacleImage
{
    /// <summary>The full image reference to test, e.g. docker.packages.octopushq.com/octopusdeploy/tentacle:9.1.0-linux.</summary>
    public const string ImageAndTagVariable = "TentacleDockerTests_ImageAndTag";

    /// <summary>Set by TeamCity to the tag of the image built earlier in the chain.</summary>
    public const string ImageTagVariable = "TentacleDockerTests_ImageTag";

    const string Repository = "docker.packages.octopushq.com/octopusdeploy/tentacle";

    public static string Resolve()
    {
        var imageAndTag = Environment.GetEnvironmentVariable(ImageAndTagVariable);
        if (!string.IsNullOrWhiteSpace(imageAndTag))
        {
            return imageAndTag;
        }

        var tag = Environment.GetEnvironmentVariable(ImageTagVariable);
        if (!string.IsNullOrWhiteSpace(tag))
        {
            return $"{Repository}:{tag}";
        }

        var hint = TeamCityDetection.IsRunningInTeamCity()
            ? $"The build should set {ImageTagVariable} to the tag of the image built earlier in the chain."
            : "Build one with ./testing/docker-linux/build-and-test-linux-docker-image.sh --skip-smoke --skip-e2e, " +
              $"then set {ImageAndTagVariable} to the image it prints.";

        throw new InvalidOperationException($"No Tentacle image to test. Set {ImageAndTagVariable} (or {ImageTagVariable}). {hint}");
    }
}
