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
/// Checks the fake Octopus Server against the Octopus.Client calls `tentacle register-with` makes, without Docker.
/// If an Octopus.Client upgrade changes what it asks for, this fails quickly and locally instead of as a container
/// that never registers.
/// </summary>
[TestFixture]
public class FakeOctopusServerContractTests
{
    FakeOctopusServer? server;
    FakeOctopusServer Server => server ?? throw new InvalidOperationException("Expected server to be set");

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        server = await FakeOctopusServer.Start(TestCertificates.ServerPublicThumbprint, new SerilogLoggerBuilder().Build());
    }

    [TearDown]
    public void TearDown()
    {
        TestContext.Out.WriteLine("Fake Octopus Server requests:");
        foreach (var request in Server.RequestLog)
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

    [Test]
    [TestCase(CommunicationStyle.TentaclePassive)]
    [TestCase(CommunicationStyle.TentacleActive)]
    public async Task OctopusClientCanRegisterATentacle(CommunicationStyle communicationStyle)
    {
        var spaceName = $"Contract-{communicationStyle}";
        Server.AddSpace(spaceName, "Development");
        var thumbprint = Guid.NewGuid().ToString("N").ToUpperInvariant();
        var machineName = $"contract-{communicationStyle}".ToLowerInvariant();

        using var client = await OctopusAsyncClient.Create(new OctopusServerEndpoint($"http://localhost:{Server.Port}", "API-FAKEFAKEFAKEFAKEFAKEFAKEFAKE"));

        // The same calls RegisterMachineCommandBase makes, in the same order.
        var rootDocument = await client.ForSystem().LoadRootDocument(CancellationToken.None);
        rootDocument.Version.Should().Be(FakeOctopusServer.ServerVersion);

        var certificate = await client.ForSystem().CertificateConfiguration.GetOctopusCertificate();
        certificate.Thumbprint.Should().Be(TestCertificates.ServerPublicThumbprint);

        var spaceRepository = await new SpaceRepositoryFactory().CreateSpaceRepository(client, spaceName);

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

        if (communicationStyle == CommunicationStyle.TentaclePassive)
        {
            operation.TentacleHostname = "contract-host";
            operation.TentaclePort = 10933;
        }
        else
        {
            operation.SubscriptionId = new Uri("poll://contracttest/");
        }

        var registered = Server.WaitForRegistration(spaceName, machineName);

        await operation.ExecuteAsync(spaceRepository);

        var machine = await registered.WaitAsync(TimeSpan.FromSeconds(10));
        Server.UnhandledRequests.Should().BeEmpty();
        machine.CommunicationStyle.Should().Be(communicationStyle.ToString());
        machine.Thumbprint.Should().Be(thumbprint);
        machine.Roles.Should().Equal("docker-test");
        machine.EnvironmentIds.Should().HaveCount(1);
        machine.Uri.Should().Be(communicationStyle == CommunicationStyle.TentaclePassive ? "https://contract-host:10933/" : "poll://contracttest/");

        // Registering again must find the existing machine by thumbprint and update it, as a restarted container would.
        await operation.ExecuteAsync(spaceRepository);
        Server.UnhandledRequests.Should().BeEmpty();
    }
}
