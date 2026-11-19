using System.Net;
using Halibut;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Docker.Tests.Integration.Setup;
using Octopus.Tentacle.Tests.Integration.Common;
using Octopus.Tentacle.Tests.Integration.Common.Logging;

namespace Octopus.Tentacle.Docker.Tests.Integration;

/// <summary>
/// The stand-in Octopus Server shared by every fixture in this assembly: the fake REST API Tentacles register with,
/// and a server HalibutRuntime that polling Tentacles connect to and that the tests use to call any Tentacle.
/// </summary>
[SetUpFixture]
public class DockerTestsSetUpFixture
{
    static FakeOctopusServer? server;
    static HalibutRuntime? halibut;
    static int pollingPort;

    public static FakeOctopusServer Server => server ?? throw new InvalidOperationException($"{nameof(DockerTestsSetUpFixture)} has not run");
    public static HalibutRuntime Halibut => halibut ?? throw new InvalidOperationException($"{nameof(DockerTestsSetUpFixture)} has not run");

    /// <summary>The port polling Tentacles connect to, i.e. what a real server exposes as 10943.</summary>
    public static int PollingPort => pollingPort;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        halibut = TestServerHalibut.BuildServerHalibutRuntime();
        // Any rather than Loopback: polling Tentacles reach us through the Docker host gateway.
        pollingPort = halibut.Listen(new IPEndPoint(IPAddress.Any, 0));

        server = await FakeOctopusServer.Start(TestCertificates.ServerPublicThumbprint, new SerilogLoggerBuilder().Build());

        // A real server trusts a Tentacle once it is registered. Polling Tentacles start connecting straight after
        // registration, so trust them as soon as they register rather than when a test gets around to it.
        server.MachineRegistered += machine =>
        {
            if (machine.Thumbprint is not null)
            {
                halibut.Trust(machine.Thumbprint);
            }
        };
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        try
        {
            if (halibut is not null)
            {
                await halibut.DisposeAsync();
            }
        }
        finally
        {
            if (server is not null)
            {
                await server.DisposeAsync();
            }
        }
    }
}
