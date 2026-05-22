using System;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Internals.Options;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Tests.Support.TestAttributes;
using Octopus.Tentacle.Util;
using Octopus.Tentacle.Watchdog;

namespace Octopus.Tentacle.Tests.Startup
{
    public class WatchdogCommandFixture
    {
        IWindowsLocalAdminRightsChecker windowsLocalAdminRightsChecker;
        WatchdogCommand command;

        [SetUp]
        public void Setup()
        {
            windowsLocalAdminRightsChecker = Substitute.For<IWindowsLocalAdminRightsChecker>();
            var log = Substitute.For<ISystemLog>();
            var watchdog = new Lazy<IWatchdog>(() => Substitute.For<IWatchdog>());

            var fileLog = Substitute.For<ILogFileOnlyLogger>();
            command = new WatchdogCommand(log, ApplicationName.Tentacle, watchdog, windowsLocalAdminRightsChecker, fileLog);
        }

        [Test]
        [WindowsTest]
        public async Task ThrowsControlledFailureExceptionWhenCreateOrDeleteIsNotSpecified()
        {
            var ex = await Assert.ThrowsAsync<ControlledFailureException>(async () => await command.StartAsync(new string[0], Substitute.For<ICommandRuntime>(), new OptionSet()));
            ex.Message.Should().Be("Invalid arguments. Please specify either --create or --delete.");
        }

        [Test]
        [LinuxTest]
        public async Task ThrowsControlledFailureWhenRunOnLinux()
        {
            var ex = await Assert.ThrowsAsync<ControlledFailureException>(async () => await command.StartAsync(new[] { "--instances", "*" }, Substitute.For<ICommandRuntime>(), new OptionSet()));
            ex.Message.Should().Be("This command is only supported on Windows.");
        }

        [Test]
        [WindowsTest]
        public async Task ChecksThatUserIsElevated()
        {
            await command.StartAsync(new[] { "--create" }, Substitute.For<ICommandRuntime>(), new OptionSet());
            windowsLocalAdminRightsChecker.Received(1).AssertIsRunningElevated();
        }
    }
}
