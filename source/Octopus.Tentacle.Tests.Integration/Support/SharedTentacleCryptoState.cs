using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using Nito.AsyncEx;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Core.Util;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    /// <summary>
    /// Initializes, once per test process, the crypto state that every Tentacle in the run shares.
    ///
    /// Parallel tests isolate their configuration directories, but the Tentacles they start share state that is
    /// created on first use and is not per instance: on Linux, the fallback machine key (/etc/octopus/machinekey,
    /// used when /etc/machine-id is unusable) and the CurrentUser/My certificate store. Creating that state for the
    /// first time from several Tentacles at once is what fails - one process can replace the key another process
    /// just encrypted its configuration with, or two processes can import the same certificate into the store
    /// concurrently. Once the state exists and is valid, reading it concurrently is fine.
    ///
    /// So rather than serializing every startup, we do one real Tentacle run up front - create an instance, import
    /// the test certificate, then read it back with show-thumbprint, which is what decrypts the configuration and
    /// loads the certificate into the store - and verify it produced the certificate we expect. Test startups then
    /// run in parallel against state that already exists.
    ///
    /// This is a mitigation for the test suite, not a fix for the underlying production behaviour: the key and
    /// certificate initialization paths themselves are unchanged.
    ///
    /// The warmed state belongs to the user that runs the Tentacle. The Tentacles the tests start are children of
    /// this process and so share its user, HOME and certificate store. Tentacles installed as a service run as
    /// whatever identity the service manager gives them, which this does not cover.
    /// </summary>
    public class SharedTentacleCryptoState
    {
        public static SharedTentacleCryptoState Instance { get; } = new(InitializeUsingTheRealTentacleExe);

        readonly Func<ILogger, CancellationToken, Task> initialize;
        readonly AsyncLock initializeLock = new();

        volatile bool isInitialized;
        volatile Exception? failure;
        int attempts;

        internal SharedTentacleCryptoState(Func<ILogger, CancellationToken, Task> initialize)
        {
            this.initialize = initialize;
        }

        /// <summary>
        /// How many times the initialization has actually been run. Only intended for tests of this class.
        /// </summary>
        internal int Attempts => Volatile.Read(ref attempts);

        /// <summary>
        /// Makes sure the shared crypto state exists and is usable, doing the work at most once.
        ///
        /// A failure is deliberately sticky: every caller is told the state could not be initialized, rather than
        /// being allowed to carry on and start Tentacles in parallel against state we know is not there. A
        /// cancelled run is not sticky, since it tells us nothing about the state itself.
        /// </summary>
        public async Task EnsureInitializedAsync(ILogger log, CancellationToken cancellationToken)
        {
            ThrowIfAlreadyFailed();
            if (isInitialized) return;

            var logger = log.ForContext<SharedTentacleCryptoState>();

            using (await initializeLock.LockAsync(cancellationToken))
            {
                ThrowIfAlreadyFailed();
                if (isInitialized) return;

                Interlocked.Increment(ref attempts);

                var initializeFor = Stopwatch.StartNew();

                try
                {
                    await initialize(logger, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    logger.Warning("Initializing the shared Tentacle crypto state was cancelled");
                    throw;
                }
                catch (Exception e)
                {
                    failure = e;
                    throw new SharedTentacleCryptoStateException("Could not initialize the crypto state shared by the Tentacles in this test run.", e);
                }

                isInitialized = true;

                logger.Information("Initialized the shared Tentacle crypto state in {DurationMs}ms", initializeFor.ElapsedMilliseconds);
            }
        }

        void ThrowIfAlreadyFailed()
        {
            var alreadyFailed = failure;

            if (alreadyFailed != null)
            {
                throw new SharedTentacleCryptoStateException(
                    "The crypto state shared by the Tentacles in this test run could not be initialized, so Tentacles cannot be started reliably.",
                    alreadyFailed);
            }
        }

        internal static async Task InitializeUsingTheRealTentacleExe(ILogger logger, CancellationToken cancellationToken)
        {
            var tentacleExe = TentacleExeFinder.FindTentacleExe();
            logger.Information("Initializing the shared Tentacle crypto state using {TentacleExe}", tentacleExe);

            using var temporaryDirectory = new TemporaryDirectory();

            var instanceName = $"tentacleit-cryptostate-{Guid.NewGuid():N}";
            var configFilePath = Path.Combine(temporaryDirectory.DirectoryPath, instanceName + ".cfg");
            var thumbprintFilePath = Path.Combine(temporaryDirectory.DirectoryPath, "thumbprint.txt");

            var environmentVariables = new Dictionary<string, string>
            {
                // Register the temporary instance under our own directory rather than the machine wide one, like
                // the Tentacle builders do, so that nothing is left behind for the tests to trip over.
                [EnvironmentVariables.TentacleMachineConfigurationHomeDirectory] = Path.Combine(temporaryDirectory.DirectoryPath, "Octopus", "Tentacle", "Instances")
            };

            try
            {
                await RunTentacle(new[] { "create-instance", "--config", configFilePath, $"--instance={instanceName}" });

                // Writes the certificate to the configuration, encrypted with the machine key, creating that key if
                // it does not exist yet.
                await RunTentacle(new[] { "import-certificate", $"--from-file={TestCertificates.TentaclePfxPath}", $"--instance={instanceName}" });

                // Reads it back, which is the part that decrypts the configuration and loads the certificate into
                // the user's certificate store. Importing on its own does not populate the store.
                await RunTentacle(new[] { "show-thumbprint", $"--export-file={thumbprintFilePath}", $"--instance={instanceName}" });

                await VerifyTheExpectedCertificateWasLoaded(thumbprintFilePath, logger, cancellationToken);
            }
            finally
            {
                // Removes the temporary instance only. The machine key and the certificate store are the shared
                // state we just created, and the rest of the run depends on them, so they are left alone.
                await DeleteTheTemporaryInstanceIgnoringFailure();
            }

            async Task RunTentacle(string[] arguments)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var exitCode = await RunTentacleCommand(arguments, cancellationToken);

                if (exitCode != 0)
                {
                    throw new Exception($"Tentacle {arguments[0]} returned a non zero exit code: {exitCode}");
                }
            }

            async Task DeleteTheTemporaryInstanceIgnoringFailure()
            {
                try
                {
                    // Deliberately not the caller's token: we still want to clean up after a cancelled or failed run.
                    using var deleteCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));

                    var exitCode = await RunTentacleCommand(new[] { "delete-instance", $"--instance={instanceName}" }, deleteCancellationTokenSource.Token);

                    if (exitCode != 0)
                    {
                        logger.Warning("Could not delete the temporary instance {InstanceName}: exit code {ExitCode}", instanceName, exitCode);
                    }
                }
                catch (Exception e)
                {
                    logger.Warning(e, "Could not delete the temporary instance {InstanceName}", instanceName);
                }
            }

            async Task<int> RunTentacleCommand(string[] arguments, CancellationToken commandCancellationToken)
            {
                var result = await Cli.Wrap(tentacleExe)
                    .WithArguments(arguments)
                    .WithEnvironmentVariables(environmentVariables)
                    .WithWorkingDirectory(temporaryDirectory.DirectoryPath)
                    .WithValidation(CommandResultValidation.None)
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => logger.Information("[Tentacle] {Output}", s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => logger.Information("[Tentacle] {Output}", s)))
                    .ExecuteAsync(commandCancellationToken);

                return result.ExitCode;
            }
        }

        static async Task VerifyTheExpectedCertificateWasLoaded(string thumbprintFilePath, ILogger logger, CancellationToken cancellationToken)
        {
            var expectedThumbprint = TestCertificates.TentaclePublicThumbprint;

            if (!File.Exists(thumbprintFilePath))
            {
                throw new Exception($"Tentacle show-thumbprint did not write a thumbprint to {thumbprintFilePath}.");
            }

            var thumbprint = (await File.ReadAllTextAsync(thumbprintFilePath, cancellationToken)).Trim();

            if (!string.Equals(thumbprint, expectedThumbprint, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception(
                    $"Tentacle loaded the certificate with thumbprint '{thumbprint}' but the tests use '{expectedThumbprint}'. " +
                    "The configuration was most likely written with one machine key and read back with another.");
            }

            VerifyTheCertificateIsInTheUsersStore(expectedThumbprint, logger);

            logger.Information("The shared machine key and certificate store are usable and hold the test certificate {Thumbprint}", thumbprint);
        }

        static void VerifyTheCertificateIsInTheUsersStore(string expectedThumbprint, ILogger logger)
        {
            // Windows keeps the Tentacle certificate in its own store, everywhere else it goes to My. See
            // CertificateEncoder.
            var storeName = PlatformDetection.IsRunningOnWindows ? "Octopus" : "My";

            using var store = new X509Store(storeName, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);

            var certificate = store.Certificates
                .Find(X509FindType.FindByThumbprint, expectedThumbprint, validOnly: false)
                .OfType<X509Certificate2>()
                .FirstOrDefault();

            if (certificate == null)
            {
                throw new Exception(
                    $"Tentacle reported the expected thumbprint but the certificate is not in the {storeName} store of {Environment.UserName}. " +
                    "Tentacles started by the tests would each have to add it themselves.");
            }

            if (StoreKeepsPrivateKeys)
            {
                if (!certificate.HasPrivateKey)
                {
                    throw new Exception($"The test certificate is in the {storeName} store of {Environment.UserName} but its private key is not readable.");
                }
            }
            else
            {
                // On Linux and macOS the Tentacle loads its certificate with X509KeyStorageFlags.EphemeralKeySet, so
                // the private key only ever lives in the loading process and the store holds the public certificate.
                // The load itself is what proves the key is usable, and show-thumbprint has just done it.
                logger.Information("The store on this platform holds the public certificate only, so the private key is not checked here");
            }
        }

        static bool StoreKeepsPrivateKeys => PlatformDetection.IsRunningOnWindows;
    }

    public class SharedTentacleCryptoStateException : Exception
    {
        public SharedTentacleCryptoStateException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
