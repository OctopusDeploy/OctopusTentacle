using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Internals.Options;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Tests.Support.TestAttributes;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Startup
{
    public class CheckServicesCommandFixture
    {
        IApplicationInstanceStore instanceStore;
        IWindowsLocalAdminRightsChecker windowsLocalAdminRightsChecker;
        CheckServicesCommand command;

        [SetUp]
        public void Setup()
        {
            instanceStore = Substitute.For<IApplicationInstanceStore>();
            windowsLocalAdminRightsChecker = Substitute.For<IWindowsLocalAdminRightsChecker>();
            var log = Substitute.For<ISystemLog>();
            var fileLog = Substitute.For<ILogFileOnlyLogger>();
            command = new CheckServicesCommand(log, instanceStore, ApplicationName.Tentacle, windowsLocalAdminRightsChecker, fileLog);
        }

        [Test]
        public async Task ThrowsControlledFailureExceptionWhenNoInstancesProvided()
        {
            var ex = await Assert.ThrowsAsync<ControlledFailureException>(async () => await command.StartAsync(new string[0], Substitute.For<ICommandRuntime>(), new OptionSet()));
            ex.Message.Should().Be("Use --instances argument to specify which instances to check. Use --instances=* to check all instances.");
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
            await command.StartAsync(new[] { "--instances", "*" }, Substitute.For<ICommandRuntime>(), new OptionSet());
            windowsLocalAdminRightsChecker.Received(1).AssertIsRunningElevated();
        }
    }
}
