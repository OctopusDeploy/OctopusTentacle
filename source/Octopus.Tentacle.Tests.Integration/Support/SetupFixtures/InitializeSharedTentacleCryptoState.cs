using System;
using System.Threading;
using Octopus.Tentacle.CommonTestUtils;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support.SetupFixtures
{
    /// <summary>
    /// Creates the crypto state the Tentacles in this run share before any test starts one, so that the test
    /// startups themselves can run in parallel. See <see cref="SharedTentacleCryptoState"/> for what is shared and
    /// why creating it concurrently is the part that fails.
    ///
    /// If this fails the whole run fails: starting Tentacles in parallel against crypto state we could not create
    /// is exactly the flakiness this avoids.
    /// </summary>
    public class InitializeSharedTentacleCryptoState : ISetupFixture
    {
        static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

        readonly CancellationTokenSource cancellationTokenSource = new();

        public void OneTimeSetUp(ILogger logger)
        {
            // Only Linux shares a machine key file between Tentacle instances, and only Linux has been seen to fail
            // this way. Windows protects the configuration per user with DPAPI instead.
            if (!PlatformDetection.IsRunningOnNix)
            {
                logger.Information("Not running on Linux, so there is no shared Tentacle crypto state to initialize");
                return;
            }

            using var timeoutCancellationTokenSource = new CancellationTokenSource(Timeout);
            using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token, timeoutCancellationTokenSource.Token);

            try
            {
                SharedTentacleCryptoState.Instance.EnsureInitializedAsync(logger, linkedCancellationTokenSource.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException e) when (timeoutCancellationTokenSource.IsCancellationRequested)
            {
                throw new TimeoutException($"Initializing the crypto state shared by the Tentacles in this test run did not complete within {Timeout}.", e);
            }
        }

        public void OneTimeTearDown(ILogger logger)
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
    }
}
