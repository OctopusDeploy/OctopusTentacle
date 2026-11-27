using System;
using System.IO;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Tests.Commands.Support;
using Octopus.Tentacle.Tests.Support;
using Octopus.Tentacle.Util;
using Octopus.Time;

namespace Octopus.Tentacle.Tests.Commands
{
    public class LivezCommandTests
    {
        [Test]
        public void Succeeds_When_Heartbeat_Is_Fresh()
        {
            using var homeDirectory = NewHomeDirectory();
            var clock = Substitute.For<IClock>();
            var command = CreateCommand(homeDirectory.DirectoryPath, clock);

            var now = new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
            WriteHeartbeatWithMtime(homeDirectory, now);
            clock.GetUtcTime().Returns(new DateTimeOffset(now.AddSeconds(5), TimeSpan.Zero));

            Action act = () => command.StartWith();

            act.Should().NotThrow();
        }

        [Test]
        public void Fails_When_Heartbeat_Is_Stale()
        {
            using var homeDirectory = NewHomeDirectory();
            var clock = Substitute.For<IClock>();
            var command = CreateCommand(homeDirectory.DirectoryPath, clock);

            var now = new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
            WriteHeartbeatWithMtime(homeDirectory, now);
            clock.GetUtcTime().Returns(new DateTimeOffset(now.AddMinutes(5), TimeSpan.Zero));

            Action act = () => command.StartWith();

            act.Should().Throw<ControlledFailureException>()
                .WithMessage("*stale*");
        }

        [Test]
        public void Fails_When_Heartbeat_File_Is_Missing()
        {
            using var homeDirectory = NewHomeDirectory();
            var clock = Substitute.For<IClock>();
            var command = CreateCommand(homeDirectory.DirectoryPath, clock);

            clock.GetUtcTime().Returns(DateTimeOffset.UtcNow);

            Action act = () => command.StartWith();

            act.Should().Throw<ControlledFailureException>()
                .WithMessage("*not found*");
        }

        [Test]
        public void Custom_MaxAge_Allows_Older_Heartbeat()
        {
            using var homeDirectory = NewHomeDirectory();
            var clock = Substitute.For<IClock>();
            var command = CreateCommand(homeDirectory.DirectoryPath, clock);

            var now = new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
            WriteHeartbeatWithMtime(homeDirectory, now);
            clock.GetUtcTime().Returns(new DateTimeOffset(now.AddSeconds(90), TimeSpan.Zero));

            Action act = () => command.StartWith("--max-age=120");

            act.Should().NotThrow();
        }

        [Test]
        public void Custom_MaxAge_Tighter_Than_Default_Fails_Sooner()
        {
            using var homeDirectory = NewHomeDirectory();
            var clock = Substitute.For<IClock>();
            var command = CreateCommand(homeDirectory.DirectoryPath, clock);

            var now = new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
            WriteHeartbeatWithMtime(homeDirectory, now);
            clock.GetUtcTime().Returns(new DateTimeOffset(now.AddSeconds(30), TimeSpan.Zero));

            Action act = () => command.StartWith("--max-age=10");

            act.Should().Throw<ControlledFailureException>()
                .WithMessage("*stale*");
        }

        [Test]
        public void Fails_When_MaxAge_Is_Not_Positive()
        {
            using var homeDirectory = NewHomeDirectory();
            var clock = Substitute.For<IClock>();
            var command = CreateCommand(homeDirectory.DirectoryPath, clock);

            var now = new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
            WriteHeartbeatWithMtime(homeDirectory, now);
            clock.GetUtcTime().Returns(new DateTimeOffset(now, TimeSpan.Zero));

            Action act = () => command.StartWith("--max-age=0");

            act.Should().Throw<ControlledFailureException>()
                .WithMessage("*positive*");
        }

        static TemporaryDirectory NewHomeDirectory()
        {
            return new TemporaryDirectory(new OctopusPhysicalFileSystem(Substitute.For<ISystemLog>()));
        }

        static void WriteHeartbeatWithMtime(TemporaryDirectory homeDirectory, DateTime mtimeUtc)
        {
            var heartbeatPath = Path.Combine(homeDirectory.DirectoryPath, LivenessHeartbeatTask.HeartbeatFileName);
            File.WriteAllBytes(heartbeatPath, Array.Empty<byte>());
            File.SetLastWriteTimeUtc(heartbeatPath, mtimeUtc);
        }

        static LivezCommand CreateCommand(string homeDirectory, IClock clock)
        {
            var homeConfiguration = Substitute.For<IHomeConfiguration>();
            homeConfiguration.HomeDirectory.Returns(homeDirectory);

            return new LivezCommand(new Lazy<IHomeConfiguration>(() => homeConfiguration),
                clock,
                UnnamedInstanceSelector(),
                Substitute.For<ISystemLog>(),
                Substitute.For<ILogFileOnlyLogger>());
        }

        static IApplicationInstanceSelector UnnamedInstanceSelector()
        {
            var selector = Substitute.For<IApplicationInstanceSelector>();
            selector.Current.Returns(_ => new ApplicationInstanceConfiguration(null, null!, null!, null!, null));
            return selector;
        }
    }
}
