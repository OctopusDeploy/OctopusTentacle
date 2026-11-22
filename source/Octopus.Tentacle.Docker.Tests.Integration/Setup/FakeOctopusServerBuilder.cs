using Octopus.Tentacle.Tests.Integration.Common.Logging;

namespace Octopus.Tentacle.Docker.Tests.Integration.Setup;

/// <summary>
/// Builds a <see cref="FakeOctopusServer"/> for one test:
/// <code>await using var server = await new FakeOctopusServerBuilder().Build();</code>
/// </summary>
public class FakeOctopusServerBuilder
{
    /// <summary>The server's only space.</summary>
    public const string SpaceName = "Default";

    /// <summary>The only environment in <see cref="SpaceName"/>.</summary>
    public const string EnvironmentName = "Development";

    public Task<FakeOctopusServer> Build()
        => FakeOctopusServer.Start(
            new Dictionary<string, string[]> { [SpaceName] = new[] { EnvironmentName } },
            new SerilogLoggerBuilder().Build());
}
