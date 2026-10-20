using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Tests.Integration.Util;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class RunningTentacleFixture
    {
        [Test]
        public async Task WhenTheTentacleFailsToStart_AnythingItLeftRunningIsCancelled()
        {
            // A Tentacle that failed to become ready should be stopped rather than left running alongside the
            // rest of the tests.
            using var temporaryDirectory = new TemporaryDirectory();
            var tokenGivenToStart = CancellationToken.None;

            await using var runningTentacle = BuildRunningTentacle(
                temporaryDirectory,
                startTentacleFunction: ct =>
                {
                    tokenGivenToStart = ct;
                    throw new Exception("Tentacle did not appear to start correctly");
                });

            Func<Task> start = () => runningTentacle.Start(CancellationToken.None);
            await start.Should().ThrowAsync<Exception>().WithMessage("Tentacle did not appear to start correctly");

            tokenGivenToStart.IsCancellationRequested.Should().BeTrue();
        }

        [Test]
        public async Task AFailedStartCanBeDisposedAndRetried()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var startAttempts = 0;
            var instanceWasDeleted = false;

            var runningTentacle = BuildRunningTentacle(
                temporaryDirectory,
                startTentacleFunction: _ =>
                {
                    if (++startAttempts == 1) throw new Exception("Tentacle did not appear to start correctly");

                    return Task.FromResult<(Task, Uri)>((Task.CompletedTask, new Uri("https://localhost:1234")));
                },
                deleteInstanceFunction: _ =>
                {
                    instanceWasDeleted = true;
                    return Task.CompletedTask;
                });

            Func<Task> start = () => runningTentacle.Start(CancellationToken.None);
            await start.Should().ThrowAsync<Exception>();

            // The failed start must not leave us looking like we are already running.
            await runningTentacle.Start(CancellationToken.None);
            runningTentacle.ServiceUri.Port.Should().Be(1234);

            await runningTentacle.DisposeAsync();
            instanceWasDeleted.Should().BeTrue();
        }

        static RunningTentacle BuildRunningTentacle(
            TemporaryDirectory temporaryDirectory,
            Func<CancellationToken, Task<(Task, Uri)>> startTentacleFunction,
            Func<CancellationToken, Task>? deleteInstanceFunction = null)
        {
            return new RunningTentacle(
                new FileInfo("Tentacle"),
                temporaryDirectory,
                startTentacleFunction,
                "thumbprint",
                "instance",
                temporaryDirectory.DirectoryPath,
                temporaryDirectory.DirectoryPath,
                deleteInstanceFunction ?? (_ => Task.CompletedTask),
                new System.Collections.Generic.Dictionary<string, string>(),
                new SerilogLoggerBuilder().Build());
        }
    }
}
