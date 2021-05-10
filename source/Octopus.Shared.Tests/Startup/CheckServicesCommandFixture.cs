using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Internals.Options;
using Octopus.Shared.Startup;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Startup
{
    public class CheckServicesCommandFixture
    {
        IApplicationInstanceRegistry instanceRegistry;
        IWindowsLocalAdminRightsChecker windowsLocalAdminRightsChecker;
        CheckServicesCommand command;

        [SetUp]
        public void Setup()
        {
            instanceRegistry = Substitute.For<IApplicationInstanceRegistry>();
            windowsLocalAdminRightsChecker = Substitute.For<IWindowsLocalAdminRightsChecker>();
            var log = Substitute.For<ISystemLog>();
            command = new CheckServicesCommand(log, instanceRegistry, ApplicationName.OctopusServer, windowsLocalAdminRightsChecker);
        }

        [Test]
        public void ThrowsControlledFailureExceptionWhenNoInstancesProvided()
        {
            var ex = Assert.Throws<ControlledFailureException>(() => command.Start(new string[0], Substitute.For<ICommandRuntime>(), new OptionSet()));
            ex.Message.Should().Be("Use --instances argument to specify which instances to check. Use --instances=* to check all instances.");
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
            command.Start(new[] { "--instances", "*" }, Substitute.For<ICommandRuntime>(), new OptionSet());
            windowsLocalAdminRightsChecker.Received(1).AssertIsRunningElevated();
        }
    }
}
