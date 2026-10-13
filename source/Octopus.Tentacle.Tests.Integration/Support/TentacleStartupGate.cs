using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.CommonTestUtils;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    /// <summary>
    /// Serializes Tentacle startup within this test process, while leaving test bodies running in parallel.
    ///
    /// Parallel tests isolate their configuration directories, but the Tentacles they start share state that is
    /// initialised on first use and is not per instance: on Linux, the fallback machine key
    /// (/etc/octopus/machinekey, used when /etc/machine-id is unusable) and the CurrentUser/My certificate store.
    /// Starting several Tentacles at once can have one process replace the key another process just encrypted its
    /// configuration with, or have two processes import the same certificate into the store at the same time.
    ///
    /// This gate is held from create-instance through certificate import, configuration and readiness, and is
    /// released before the Tentacle is handed back to the test. It is deliberately not taken on the deletion path:
    /// a failed start disposes its RunningTentacle while the gate is still held, so making it reentrant-free keeps
    /// cleanup from deadlocking against the build that owns the gate.
    ///
    /// This is a mitigation for the test suite, not a fix for the underlying production behaviour: the key and
    /// certificate initialisation paths themselves are unchanged.
    ///
    /// The gate coordinates builder startup within one test process. Independent processes would need additional
    /// coordination only if they share the same mutable key/store namespace; whether they do depends on how the
    /// tests are hosted, so it is worth checking before assuming either way.
    /// </summary>
    public class TentacleStartupGate
    {
        /// <summary>
        /// Set to "true" to run with startup serialization disabled, so the same CI image can be used to measure
        /// cold parallel startup against serialized startup.
        /// </summary>
        public const string DisableEnvironmentVariableName = "OCTOPUS_TEST_DISABLE_TENTACLE_STARTUP_SERIALIZATION";

        public static TentacleStartupGate Instance { get; } = new(IsRequiredOnThisPlatform());

        readonly SemaphoreSlim gate = new(1, 1);
        readonly bool isEnabled;

        internal TentacleStartupGate(bool isEnabled)
        {
            this.isEnabled = isEnabled;
        }

        /// <summary>
        /// True while a startup sequence holds the gate. Only intended for tests of the gate itself.
        /// </summary>
        internal bool IsHeld => isEnabled && gate.CurrentCount == 0;

        static bool IsRequiredOnThisPlatform()
        {
            if (!PlatformDetection.IsRunningOnNix) return false;

            var disabled = Environment.GetEnvironmentVariable(DisableEnvironmentVariableName);
            return !string.Equals(disabled, "true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Waits for exclusive access to Tentacle startup. Dispose the returned lease to let the next startup run.
        /// If the wait is cancelled the gate is not taken.
        /// </summary>
        public async Task<IDisposable> AcquireAsync(string instanceName, ILogger log, CancellationToken cancellationToken)
        {
            if (!isEnabled) return new NoOpLease();

            var logger = log.ForContext<TentacleStartupGate>();
            var queuedFor = Stopwatch.StartNew();

            logger.Information("Waiting for the Tentacle startup gate to start {InstanceName}", instanceName);
            await gate.WaitAsync(cancellationToken);
            queuedFor.Stop();

            logger.Information(
                "Acquired the Tentacle startup gate for {InstanceName} after queuing for {QueueDurationMs}ms",
                instanceName,
                queuedFor.ElapsedMilliseconds);

            return new Lease(this, instanceName, logger);
        }

        void Release(string instanceName, TimeSpan heldFor, ILogger logger)
        {
            gate.Release();

            logger.Information(
                "Released the Tentacle startup gate for {InstanceName} after holding it for {HoldDurationMs}ms",
                instanceName,
                (long)heldFor.TotalMilliseconds);
        }

        class Lease : IDisposable
        {
            readonly TentacleStartupGate owner;
            readonly string instanceName;
            readonly ILogger logger;
            readonly Stopwatch heldFor = Stopwatch.StartNew();
            int disposed;

            public Lease(TentacleStartupGate owner, string instanceName, ILogger logger)
            {
                this.owner = owner;
                this.instanceName = instanceName;
                this.logger = logger;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) != 0) return;

                heldFor.Stop();
                owner.Release(instanceName, heldFor.Elapsed, logger);
            }
        }

        class NoOpLease : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
