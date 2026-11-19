using FluentAssertions;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.Client.Operations;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Docker.Tests.Integration.Setup;
using Octopus.Tentacle.Tests.Integration.Common.Logging;

namespace Octopus.Tentacle.Docker.Tests.Integration;

/// <summary>
/// Checks the fake Octopus Server against Octopus.Client, without Docker: it makes the calls RegisterMachineCommandBase
/// makes (for deployment targets and workers, with each way of authenticating), in the same order, through the same
/// Octopus.Client operations. If an Octopus.Client upgrade changes what it asks for, this fails quickly and locally
/// instead of as a container that never registers.
///
/// It copies RegisterMachineCommandBase's sequence rather than running the command (which needs root on Linux), so a
/// change to the command itself is only caught by the Docker tests.
/// </summary>
[TestFixture]
public class FakeOctopusServerContractTests
{
    FakeOctopusServer? server;
    FakeOctopusServer Server => server ?? throw new InvalidOperationException("Expected server to be set");

    DateTimeOffset testStarted;
    string? currentSpaceName;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        server = await FakeOctopusServer.Start(TestCertificates.ServerPublicThumbprint, new SerilogLoggerBuilder().Build());
    }

    [SetUp]
    public void SetUp()
    {
        testStarted = DateTimeOffset.UtcNow;
        currentSpaceName = null;
    }

    [TearDown]
    public void TearDown()
    {
        if (currentSpaceName is null)
        {
            return;
        }

        TestContext.Out.WriteLine($"Fake Octopus Server requests for {currentSpaceName}:");
        foreach (var request in Server.RequestLogFor(currentSpaceName, testStarted))
        {
            TestContext.Out.WriteLine("  " + request);
        }
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (server is not null)
        {
            await server.DisposeAsync();
        }
    }

    public enum Authentication
    {
        ApiKey,
        BearerToken,
        UsernamePassword
    }

    const string ApiKey = "API-FAKEFAKEFAKEFAKEFAKEFAKEFAKE";
    const string BearerToken = "fake-bearer-token";
    const string Username = "contract-user";
    const string Password = "contract password with $pecial \"characters\"";

    [Test]
    [TestCase(CommunicationStyle.TentaclePassive)]
    [TestCase(CommunicationStyle.TentacleActive)]
    public async Task OctopusClientCanRegisterATentacle(CommunicationStyle communicationStyle)
    {
        var (spaceName, machineName, thumbprint) = NewSpace($"Contract-{communicationStyle}");
        using var client = await Connect(Authentication.ApiKey);
        var spaceRepository = await PrepareToRegister(client, spaceName);

        var operation = new RegisterMachineOperation
        {
            MachineName = machineName,
            Environments = new[] { "Development" },
            Roles = new[] { "docker-test" },
            Tenants = Array.Empty<string>(),
            TenantTags = Array.Empty<string>(),
            TenantedDeploymentParticipation = TenantedDeploymentMode.Untenanted,
            MachinePolicy = "Default Machine Policy",
            AllowOverwrite = true,
            CommunicationStyle = communicationStyle,
            TentacleThumbprint = thumbprint
        };
        SetEndpoint(operation, communicationStyle);

        var machine = await RegisterTwice(spaceName, machineName, () => operation.ExecuteAsync(spaceRepository));

        machine.IsWorker.Should().BeFalse();
        machine.Authentication.Should().Be("api key " + ApiKey);
        machine.CommunicationStyle.Should().Be(communicationStyle.ToString());
        machine.Thumbprint.Should().Be(thumbprint);
        machine.Roles.Should().Equal("docker-test");
        machine.EnvironmentIds.Should().HaveCount(1);
        machine.Uri.Should().Be(ExpectedUri(communicationStyle));
    }

    [Test]
    [TestCase(CommunicationStyle.TentaclePassive)]
    [TestCase(CommunicationStyle.TentacleActive)]
    public async Task OctopusClientCanRegisterAWorker(CommunicationStyle communicationStyle)
    {
        var (spaceName, machineName, thumbprint) = NewSpace($"ContractWorker-{communicationStyle}");
        using var client = await Connect(Authentication.ApiKey);
        var spaceRepository = await PrepareToRegister(client, spaceName);

        // RegisterWorkerCommandBase passes the pools; the rest comes from RegisterMachineCommandBase.
        var operation = new RegisterWorkerOperation
        {
            MachineName = machineName,
            WorkerPools = new[] { FakeOctopusServer.DefaultWorkerPoolName },
            MachinePolicy = "Default Machine Policy",
            AllowOverwrite = true,
            CommunicationStyle = communicationStyle,
            TentacleThumbprint = thumbprint
        };
        SetEndpoint(operation, communicationStyle);

        var worker = await RegisterTwice(spaceName, machineName, () => operation.ExecuteAsync(spaceRepository));

        worker.IsWorker.Should().BeTrue();
        worker.WorkerPoolIds.Should().Equal(FakeOctopusServer.DefaultWorkerPoolId);
        worker.CommunicationStyle.Should().Be(communicationStyle.ToString());
        worker.Thumbprint.Should().Be(thumbprint);
        worker.Uri.Should().Be(ExpectedUri(communicationStyle));
    }

    [Test]
    [TestCase(Authentication.BearerToken, "bearer token " + BearerToken)]
    [TestCase(Authentication.UsernamePassword, "user " + Username + " with password " + Password)]
    public async Task OctopusClientAuthenticatesWithTheOtherKindsOfCredential(Authentication authentication, string expected)
    {
        var (spaceName, machineName, thumbprint) = NewSpace($"ContractAuth-{authentication}");
        using var client = await Connect(authentication);
        var spaceRepository = await PrepareToRegister(client, spaceName);

        var operation = new RegisterMachineOperation
        {
            MachineName = machineName,
            Environments = new[] { "Development" },
            Roles = new[] { "docker-test" },
            Tenants = Array.Empty<string>(),
            TenantTags = Array.Empty<string>(),
            TenantedDeploymentParticipation = TenantedDeploymentMode.Untenanted,
            MachinePolicy = "Default Machine Policy",
            AllowOverwrite = true,
            CommunicationStyle = CommunicationStyle.TentaclePassive,
            TentacleThumbprint = thumbprint
        };
        SetEndpoint(operation, CommunicationStyle.TentaclePassive);

        var machine = await RegisterTwice(spaceName, machineName, () => operation.ExecuteAsync(spaceRepository));

        machine.Authentication.Should().Be(expected);
    }

    (string SpaceName, string MachineName, string Thumbprint) NewSpace(string spaceName)
    {
        currentSpaceName = spaceName;
        Server.AddSpace(spaceName, "Development");
        return (spaceName, spaceName.ToLowerInvariant(), Guid.NewGuid().ToString("N").ToUpperInvariant());
    }

    /// <summary>As OctopusClientInitializer does: an API key or bearer token on every request, or a sign-in.</summary>
    async Task<IOctopusAsyncClient> Connect(Authentication authentication)
    {
        var url = $"http://localhost:{Server.Port}";
        switch (authentication)
        {
            case Authentication.ApiKey:
                return await OctopusAsyncClient.Create(OctopusServerEndpoint.CreateWithApiKey(url, ApiKey));
            case Authentication.BearerToken:
                return await OctopusAsyncClient.Create(OctopusServerEndpoint.CreateWithBearerToken(url, BearerToken));
            default:
                var client = await OctopusAsyncClient.Create(new OctopusServerEndpoint(url));
                await client.Repository.Users.SignIn(new LoginCommand { Username = Username, Password = Password });
                return client;
        }
    }

    /// <summary>The calls RegisterMachineCommandBase makes before registering, in the same order.</summary>
    async Task<IOctopusSpaceAsyncRepository> PrepareToRegister(IOctopusAsyncClient client, string spaceName)
    {
        var rootDocument = await client.ForSystem().LoadRootDocument(CancellationToken.None);
        rootDocument.Version.Should().Be(FakeOctopusServer.ServerVersion);

        var certificate = await client.ForSystem().CertificateConfiguration.GetOctopusCertificate();
        certificate.Thumbprint.Should().Be(TestCertificates.ServerPublicThumbprint);

        return await new SpaceRepositoryFactory().CreateSpaceRepository(client, spaceName);
    }

    static void SetEndpoint(RegisterMachineOperationBase operation, CommunicationStyle communicationStyle)
    {
        if (communicationStyle == CommunicationStyle.TentaclePassive)
        {
            operation.TentacleHostname = "contract-host";
            operation.TentaclePort = 10933;
        }
        else
        {
            operation.SubscriptionId = new Uri("poll://contracttest/");
        }
    }

    static string ExpectedUri(CommunicationStyle communicationStyle)
        => communicationStyle == CommunicationStyle.TentaclePassive ? "https://contract-host:10933/" : "poll://contracttest/";

    /// <summary>
    /// Registers, then registers again: the second must find the existing machine and update it, as a container
    /// re-run with the same configuration would.
    /// </summary>
    async Task<RegisteredMachine> RegisterTwice(string spaceName, string machineName, Func<Task> register)
    {
        var registered = Server.WaitForRegistration(spaceName, machineName);

        await register();
        var machine = await registered.WaitAsync(TimeSpan.FromSeconds(10));
        Server.UnhandledRequests.Should().BeEmpty();

        await register();
        Server.UnhandledRequests.Should().BeEmpty();
        Server.RegistrationCount(spaceName, machineName).Should().Be(2);

        return machine;
    }
}
