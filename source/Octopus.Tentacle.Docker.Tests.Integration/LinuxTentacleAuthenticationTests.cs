using FluentAssertions;
using Octopus.Tentacle.Docker.Tests.Integration.Setup;
using Octopus.Tentacle.Tests.Integration.Common.FakeOctopus;

namespace Octopus.Tentacle.Docker.Tests.Integration;

public enum DockerAuthentication
{
    ApiKey,
    UsernamePassword,
    BearerToken
}

/// <summary>
/// Registers with each kind of credential the image accepts. The fake server accepts any credentials, so this checks
/// the values reach register-with intact, not that authentication works. Neither depends on how Tentacle
/// communicates, so these only run as listening Tentacles.
/// </summary>
public class LinuxTentacleAuthenticationTests
{
    const string Username = "tentacle-docker-tests";

    // Spaces, quotes and a dollar sign: what a shell would mangle if configure-tentacle.sh did not quote it.
    const string Password = "Pa$$ w0rd \"with\" 'quotes'";

    const string BearerToken = "fake.bearer-token_for-docker-tests";

    /// <summary>LinuxTentacleImageTests checks the API key, which is the default.</summary>
    [Test]
    [TestCase(DockerAuthentication.UsernamePassword, "user " + Username + " with password " + Password)]
    [TestCase(DockerAuthentication.BearerToken, "bearer token " + BearerToken)]
    public async Task TentacleRegistersWithTheGivenCredentials(DockerAuthentication authentication, string expected)
    {
        await using var server = await new FakeOctopusServerBuilder().Build();
        await using var tentacle = await TentacleAuthenticatingWith(server, authentication).Build();

        tentacle.RegisteredMachine.Authentication.Should().Be(expected);
    }

    [Test]
    [TestCase(DockerAuthentication.ApiKey)]
    [TestCase(DockerAuthentication.UsernamePassword)]
    [TestCase(DockerAuthentication.BearerToken)]
    public async Task RegistrationCredentialsAreNotVisibleToScripts(DockerAuthentication authentication)
    {
        await using var server = await new FakeOctopusServerBuilder().Build();
        await using var tentacle = await TentacleAuthenticatingWith(server, authentication).Build();

        await tentacle.AssertRegistrationCredentialsAreNotVisibleToScripts();
    }

    static TentacleContainerBuilder TentacleAuthenticatingWith(FakeOctopusServer server, DockerAuthentication authentication)
    {
        var builder = new TentacleContainerBuilder(server);
        return authentication switch
        {
            DockerAuthentication.ApiKey => builder,
            DockerAuthentication.UsernamePassword => builder.WithUsernameAndPassword(Username, Password),
            DockerAuthentication.BearerToken => builder.WithBearerToken(BearerToken),
            _ => throw new ArgumentOutOfRangeException(nameof(authentication), authentication, null)
        };
    }
}
