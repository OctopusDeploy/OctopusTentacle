using System;
using System.Reflection;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Internals.Options;
using Octopus.Shared.Services;
using Octopus.Shared.Startup;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Startup
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
            command = new WatchdogCommand(log, ApplicationName.OctopusServer, watchdog, windowsLocalAdminRightsChecker, fileLog);
        }

        [Test]
        [WindowsTest]
        public void ThrowsControlledFailureExceptionWhenCreateOrDeleteIsNotSpecified()
        {
            var ex = Assert.Throws<ControlledFailureException>(() => command.Start(new string[0], Substitute.For<ICommandRuntime>(), new OptionSet()));
            ex.Message.Should().Be("Invalid arguments. Please specify either --create or --delete.");
        }

        [Test]
        [LinuxTest]
        public void ThrowsControlledFailureWhenRunOnLinux()
        {
            var ex = Assert.Throws<ControlledFailureException>(() => command.Start(new[] { "--instances", "*" }, Substitute.For<ICommandRuntime>(), new OptionSet()));
            ex.Message.Should().Be("This command is only supported on Windows.");
        }

        [Test]
        [WindowsTest]
        public void ChecksThatUserIsElevated()
        {
            command.Start(new [] { "--create" }, Substitute.For<ICommandRuntime>(), new OptionSet());
            windowsLocalAdminRightsChecker.Received(1).AssertIsRunningElevated();
        }
    }
}
