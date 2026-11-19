using FluentAssertions;
using Octopus.Tentacle.Docker.Tests.Integration.Setup;

namespace Octopus.Tentacle.Docker.Tests.Integration;

public enum DockerAuthentication
{
    UsernamePassword,
    BearerToken
}

/// <summary>
/// Registers with the image's other two kinds of credential (the other fixtures use ServerApiKey). The fake server
/// accepts any credentials, so this checks the values reach register-with intact, not that authentication works.
/// Authentication does not depend on how Tentacle communicates, so these only run as listening Tentacles.
/// </summary>
[TestFixture(DockerAuthentication.UsernamePassword)]
[TestFixture(DockerAuthentication.BearerToken)]
public class LinuxTentacleAuthenticationTests : TentacleContainerFixture
{
    const string Username = "tentacle-docker-tests";

    // Spaces, quotes and a dollar sign: what a shell would mangle if configure-tentacle.sh did not quote it.
    const string Password = "Pa$$ w0rd \"with\" 'quotes'";

    const string BearerToken = "fake.bearer-token_for-docker-tests";

    readonly DockerAuthentication authentication;

    public LinuxTentacleAuthenticationTests(DockerAuthentication authentication)
        : base(TentacleCommunicationStyle.Listening, $"Auth{authentication}")
    {
        this.authentication = authentication;
    }

    protected override void ConfigureContainer(IDictionary<string, string> environment)
    {
        environment.Remove("ServerApiKey");

        if (authentication == DockerAuthentication.UsernamePassword)
        {
            environment["ServerUsername"] = Username;
            environment["ServerPassword"] = Password;
        }
        else
        {
            environment["BearerToken"] = BearerToken;
        }
    }

    [Test]
    public void TentacleRegistersWithTheGivenCredentials()
    {
        var expected = authentication == DockerAuthentication.UsernamePassword
            ? $"user {Username} with password {Password}"
            : $"bearer token {BearerToken}";

        RegisteredMachine.Authentication.Should().Be(expected);
    }

    [Test]
    public Task RegistrationCredentialsAreNotVisibleToScripts() => AssertRegistrationCredentialsAreNotVisibleToScripts();
}
