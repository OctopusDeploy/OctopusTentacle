using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Tests.Integration.Support.TestAttributes;
using Octopus.Tentacle.Tests.Integration.Util;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class SharedTentacleCryptoStateFixture
    {
        [Test]
        public async Task TheStateIsOnlyInitializedOnceNoMatterHowManyCallersAsk()
        {
            var initializing = new SemaphoreSlim(0, 1);
            var sharedState = new SharedTentacleCryptoState(async (_, _) =>
            {
                // Make sure the callers really do overlap.
                await initializing.WaitAsync(CancellationToken.None);
            });

            var callers = Enumerable.Range(0, 5)
                .Select(_ => Task.Run(() => sharedState.EnsureInitializedAsync(Logger, CancellationToken.None)))
                .ToArray();

            initializing.Release();
            await Task.WhenAll(callers);

            // And a caller that turns up after everyone else has finished still does not redo the work.
            await sharedState.EnsureInitializedAsync(Logger, CancellationToken.None);

            sharedState.Attempts.Should().Be(1);
        }

        [Test]
        public async Task AFailureIsReportedToEveryCallerAndTheStateIsNotRetried()
        {
            var sharedState = new SharedTentacleCryptoState((_, _) => throw new Exception("the machine key could not be read"));

            Func<Task> initialize = () => sharedState.EnsureInitializedAsync(Logger, CancellationToken.None);

            // Nobody gets to carry on and start a Tentacle as if the state was there.
            (await initialize.Should().ThrowAsync<SharedTentacleCryptoStateException>())
                .WithInnerException<Exception>().WithMessage("the machine key could not be read");

            (await initialize.Should().ThrowAsync<SharedTentacleCryptoStateException>())
                .WithInnerException<Exception>().WithMessage("the machine key could not be read");

            sharedState.Attempts.Should().Be(1);
        }

        [Test]
        public async Task ACancelledInitializationIsNotTreatedAsAFailedOne()
        {
            var initializeAttempts = 0;
            var sharedState = new SharedTentacleCryptoState((_, cancellationToken) =>
            {
                if (++initializeAttempts == 1) cancellationToken.ThrowIfCancellationRequested();

                return Task.CompletedTask;
            });

            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();

            Func<Task> initializeCancelled = () => sharedState.EnsureInitializedAsync(Logger, cancelled.Token);
            await initializeCancelled.Should().ThrowAsync<OperationCanceledException>();

            // Cancelling tells us nothing about the state itself, so the next caller gets to try again.
            await sharedState.EnsureInitializedAsync(Logger, CancellationToken.None);

            sharedState.Attempts.Should().Be(2);
        }

        [Test]
        [LinuxTest]
        public async Task TheRealInitializationLeavesTheTestCertificateInTheUsersStore()
        {
            // This is the work the setup fixture does before any test starts a Tentacle. Running it again here is
            // safe: it is the same create instance, import, read back and clean up against a temporary instance.
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(5));

            await SharedTentacleCryptoState.InitializeUsingTheRealTentacleExe(Logger, cancellationTokenSource.Token);

            using var store = new X509Store("My", StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);

            store.Certificates
                .Find(X509FindType.FindByThumbprint, TestCertificates.TentaclePublicThumbprint, validOnly: false)
                .Should().NotBeEmpty("the Tentacles the tests start should find the certificate already in the store");
        }

        static ILogger Logger => new SerilogLoggerBuilder().Build().ForContext<SharedTentacleCryptoStateFixture>();
    }
}
