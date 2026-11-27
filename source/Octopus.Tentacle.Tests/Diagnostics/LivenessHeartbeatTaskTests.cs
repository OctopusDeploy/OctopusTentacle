using System;
using System.IO;
using System.Threading;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Diagnostics
{
    public class LivenessHeartbeatTaskTests
    {
        [Test]
        public void Touches_Heartbeat_File_On_First_Tick()
        {
            using var home = new HeartbeatHome();
            var task = home.CreateTask(TimeSpan.FromMilliseconds(50));

            task.Start();
            try
            {
                WaitUntil(() => File.Exists(home.HeartbeatPath), timeout: TimeSpan.FromSeconds(2))
                    .Should().BeTrue("the heartbeat file should be created on the first tick");
            }
            finally
            {
                task.Stop();
            }
        }

        [Test]
        public void Refreshes_Heartbeat_File_Mtime_On_Subsequent_Ticks()
        {
            using var home = new HeartbeatHome();
            var task = home.CreateTask(TimeSpan.FromMilliseconds(50));

            task.Start();
            try
            {
                WaitUntil(() => File.Exists(home.HeartbeatPath), TimeSpan.FromSeconds(2))
                    .Should().BeTrue();
                var firstMtime = File.GetLastWriteTimeUtc(home.HeartbeatPath);

                WaitUntil(() => File.GetLastWriteTimeUtc(home.HeartbeatPath) > firstMtime, TimeSpan.FromSeconds(2))
                    .Should().BeTrue("a subsequent tick should refresh the mtime");
            }
            finally
            {
                task.Stop();
            }
        }

        [Test]
        public void Survives_Io_Errors_And_Logs_A_Warning()
        {
            using var home = new HeartbeatHome();
            var log = Substitute.For<ISystemLog>();
            var unwritableHome = Path.Combine(home.HomeDirectory, "does", "not", "exist");
            var homeConfig = Substitute.For<IHomeConfiguration>();
            homeConfig.HomeDirectory.Returns(unwritableHome);

            var task = new LivenessHeartbeatTask(homeConfig, log, TimeSpan.FromMilliseconds(50));

            task.Start();
            try
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(200));
                log.ReceivedWithAnyArgs().Warn(Arg.Any<Exception>(), Arg.Any<string>());
            }
            finally
            {
                task.Stop();
            }

            File.Exists(Path.Combine(unwritableHome, LivenessHeartbeatTask.HeartbeatFileName))
                .Should().BeFalse();
        }

        [Test]
        public void Exits_Cleanly_When_Home_Directory_Is_Not_Configured()
        {
            var log = Substitute.For<ISystemLog>();
            var homeConfig = Substitute.For<IHomeConfiguration>();
            homeConfig.HomeDirectory.Returns((string?)null);

            var task = new LivenessHeartbeatTask(homeConfig, log, TimeSpan.FromMilliseconds(50));

            task.Start();
            try
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(200));
                log.ReceivedWithAnyArgs().Error(Arg.Any<string>());
            }
            finally
            {
                task.Stop();
            }
        }

        class HeartbeatHome : IDisposable
        {
            public HeartbeatHome()
            {
                var fileSystem = new OctopusPhysicalFileSystem(Substitute.For<ISystemLog>());
                HomeDirectory = fileSystem.CreateTemporaryDirectory();
                HeartbeatPath = Path.Combine(HomeDirectory, LivenessHeartbeatTask.HeartbeatFileName);
            }

            public string HomeDirectory { get; }
            public string HeartbeatPath { get; }

            public LivenessHeartbeatTask CreateTask(TimeSpan tickInterval)
            {
                var homeConfig = Substitute.For<IHomeConfiguration>();
                homeConfig.HomeDirectory.Returns(HomeDirectory);
                return new LivenessHeartbeatTask(homeConfig, Substitute.For<ISystemLog>(), tickInterval);
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(HomeDirectory))
                        Directory.Delete(HomeDirectory, recursive: true);
                }
                catch
                {
                    // best effort cleanup
                }
            }
        }

        static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return true;
                Thread.Sleep(20);
            }
            return condition();
        }
    }
}
