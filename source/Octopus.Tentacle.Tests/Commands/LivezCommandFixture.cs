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
using Octopus.Tentacle.Util;
using Octopus.Time;

namespace Octopus.Tentacle.Tests.Commands
{
    [TestFixture]
    public class LivezCommandFixture : CommandFixture<LivezCommand>
    {
        string homeDirectory = null!;
        string heartbeatPath = null!;
        IHomeConfiguration homeConfiguration = null!;
        IClock clock = null!;
        IApplicationInstanceSelector instanceSelector = null!;
        OctopusPhysicalFileSystem fileSystem = null!;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            fileSystem = new OctopusPhysicalFileSystem(Substitute.For<ISystemLog>());
            homeDirectory = fileSystem.CreateTemporaryDirectory();
            heartbeatPath = Path.Combine(homeDirectory, LivenessHeartbeatTask.HeartbeatFileName);

            homeConfiguration = Substitute.For<IHomeConfiguration>();
            homeConfiguration.HomeDirectory.Returns(homeDirectory);

            clock = Substitute.For<IClock>();

            instanceSelector = Substitute.For<IApplicationInstanceSelector>();
            instanceSelector.Current.Returns(_ => new ApplicationInstanceConfiguration(null, null!, null!, null!));

            Command = new LivezCommand(
                new Lazy<IHomeConfiguration>(() => homeConfiguration),
                clock,
                instanceSelector,
                Substitute.For<ISystemLog>(),
                Substitute.For<ILogFileOnlyLogger>());
        }

        [TearDown]
        public void TearDownAfterEachTest()
        {
            try
            {
                if (Directory.Exists(homeDirectory))
                    Directory.Delete(homeDirectory, recursive: true);
            }
            catch
            {
                // best effort cleanup
            }
        }

        [Test]
        public void Succeeds_When_Heartbeat_Is_Fresh()
        {
            var now = new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
            WriteHeartbeatWithMtime(now);
            clock.GetUtcTime().Returns(new DateTimeOffset(now.AddSeconds(5), TimeSpan.Zero));

            Action act = () => Start();

            act.Should().NotThrow();
        }

        [Test]
        public void Fails_When_Heartbeat_Is_Stale()
        {
            var now = new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
            WriteHeartbeatWithMtime(now);
            clock.GetUtcTime().Returns(new DateTimeOffset(now.AddMinutes(5), TimeSpan.Zero));

            Action act = () => Start();

            act.Should().Throw<ControlledFailureException>()
                .WithMessage("*stale*");
        }

        [Test]
        public void Fails_When_Heartbeat_File_Is_Missing()
        {
            clock.GetUtcTime().Returns(DateTimeOffset.UtcNow);

            Action act = () => Start();

            act.Should().Throw<ControlledFailureException>()
                .WithMessage("*not found*");
        }

        [Test]
        public void Custom_MaxAge_Allows_Older_Heartbeat()
        {
            var now = new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
            WriteHeartbeatWithMtime(now);
            clock.GetUtcTime().Returns(new DateTimeOffset(now.AddSeconds(90), TimeSpan.Zero));

            Action act = () => Start("--max-age=120");

            act.Should().NotThrow();
        }

        [Test]
        public void Custom_MaxAge_Tighter_Than_Default_Fails_Sooner()
        {
            var now = new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
            WriteHeartbeatWithMtime(now);
            clock.GetUtcTime().Returns(new DateTimeOffset(now.AddSeconds(30), TimeSpan.Zero));

            Action act = () => Start("--max-age=10");

            act.Should().Throw<ControlledFailureException>()
                .WithMessage("*stale*");
        }

        [Test]
        public void Fails_When_MaxAge_Is_Not_Positive()
        {
            var now = new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
            WriteHeartbeatWithMtime(now);
            clock.GetUtcTime().Returns(new DateTimeOffset(now, TimeSpan.Zero));

            Action act = () => Start("--max-age=0");

            act.Should().Throw<ControlledFailureException>()
                .WithMessage("*positive*");
        }

        void WriteHeartbeatWithMtime(DateTime mtimeUtc)
        {
            File.WriteAllBytes(heartbeatPath, Array.Empty<byte>());
            File.SetLastWriteTimeUtc(heartbeatPath, mtimeUtc);
        }
    }
}
