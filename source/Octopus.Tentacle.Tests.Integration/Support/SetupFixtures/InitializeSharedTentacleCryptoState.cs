using Octopus.Tentacle.CommonTestUtils;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support.SetupFixtures
{
    /// <summary>
    /// Creates the crypto state Tentacles share before any test starts one, so test startups can
    /// run in parallel. If this fails the whole run fails, since parallel startups against missing
    /// state are the flakiness it avoids.
    /// </summary>
    public class InitializeSharedTentacleCryptoState : ISetupFixture
    {
        public void OneTimeSetUp(ILogger logger)
        {
            // Windows encrypts configuration with DPAPI rather than a shared key file, and has not
            // been seen to fail this way.
            if (!PlatformDetection.IsRunningOnNix)
            {
                return;
            }

            SharedTentacleCryptoState.Initialize(logger);
        }

        public void OneTimeTearDown(ILogger logger)
        {
        }
    }
}
