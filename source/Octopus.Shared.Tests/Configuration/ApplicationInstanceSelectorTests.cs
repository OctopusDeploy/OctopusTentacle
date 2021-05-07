using System;
using System.Collections.Generic;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Shared.Configuration;
using Octopus.Shared.Configuration.EnvironmentVariableMappings;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Startup;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Configuration
{
    [TestFixture]
    public class ApplicationInstanceSelectorTests
    {
        static readonly object ConfigurationStore = Substitute.For<IApplicationConfigurationStrategy, IApplicationConfigurationWithMultipleInstances>();
        static readonly IApplicationConfigurationStrategy OtherStrategy = Substitute.For<IApplicationConfigurationStrategy>();

        [Test]
        public void LoadInstanceThrowsWhenNoInstanceNamePassedAndNoInstancesConfigured()
        {
            var selector = GetApplicationInstanceSelector(new List<ApplicationInstanceRecord>(), string.Empty);
            ((IApplicationConfigurationStrategy)ConfigurationStore).LoadedConfiguration(Arg.Any<ApplicationRecord>()).Returns((IAggregatableKeyValueStore)null);
            OtherStrategy.LoadedConfiguration(Arg.Any<ApplicationRecord>()).Returns((IAggregatableKeyValueStore)null);
            ((Action)(() => selector.LoadInstance()))
                .Should()
                .Throw<ControlledFailureException>()
                .WithMessage("There are no instances of OctopusServer configured on this machine. Please run the setup wizard, configure an instance using the command-line interface, specify a configuration file, or set the required environment variables.");
        }

        [Test]
        public void LoadInstanceDoesNotThrowsWhenNoInstanceNamePassedAndNoInstancesConfiguredButAnotherStrategyCanBeLoaded()
        {
            var selector = GetApplicationInstanceSelector(new List<ApplicationInstanceRecord>(), string.Empty);
            ((IApplicationConfigurationStrategy)ConfigurationStore).LoadedConfiguration(Arg.Any<ApplicationRecord>()).Returns((IAggregatableKeyValueStore)null);

            OtherStrategy.LoadedConfiguration(Arg.Any<ApplicationRecord>()).Returns(new InMemoryKeyValueStore(Substitute.For<IMapEnvironmentValuesToConfigItems>()));

            selector.CanLoadCurrentInstance().Should().BeTrue("because the in memory config should be used");
            selector.GetCurrentName().Should().BeNull("in memory config should have no instance name");
            selector.GetCurrentConfiguration().Should().NotBeNull("in memory config should available");
            selector.GetWritableCurrentConfiguration().Should().BeAssignableTo<DoNotAllowWritesInThisModeKeyValueStore>("in memory config means no writes");
        }

        [Test]
        public void LoadInstanceThrowsWhenNoInstanceNameIsPassedAndMoreThanOneInstanceWithNoDefaultInstance()
        {
            var instanceRecords = new List<ApplicationInstanceRecord>
            {
                new ApplicationInstanceRecord("My instance", "c:\\temp\\1.config"),
                new ApplicationInstanceRecord("instance 2", "c:\\temp\\2.config")
            };
            var selector = GetApplicationInstanceSelector(instanceRecords, string.Empty);
            ((Action)(() => selector.LoadInstance()))
                .Should()
                .Throw<ControlledFailureException>()
                .WithMessage("There is more than one instance of OctopusServer configured on this machine. Please pass --instance=INSTANCENAME when invoking this command to target a specific instance. Available instances: instance 2, My instance.");
        }

        [Test]
        public void LoadInstanceReturnsDefaultInstanceWhenNoInstanceNameIsPassedAndThereAreMultipleInstancesAndOneIsTheDefaultInstance()
        {
            var defaultName = ApplicationInstanceRecord.GetDefaultInstance(ApplicationName.OctopusServer);
            var instanceRecords = new List<ApplicationInstanceRecord>
            {
                new ApplicationInstanceRecord(ApplicationInstanceRecord.GetDefaultInstance(ApplicationName.OctopusServer), "c:\\temp\\0.config"),
                new ApplicationInstanceRecord("My instance", "c:\\temp\\1.config"),
                new ApplicationInstanceRecord("instance 2", "c:\\temp\\2.config")
            };
            var selector = GetApplicationInstanceSelector(instanceRecords, string.Empty);
            selector.LoadInstance().InstanceName.Should().Be(defaultName);
        }

        [Test]
        public void LoadInstanceReturnsOnlyInstanceWhenNoInstanceNameIsPassedAndOnlyOneInstance()
        {
            var instanceRecords = new List<ApplicationInstanceRecord> { new ApplicationInstanceRecord("instance 2", "c:\\temp\\2.config") };
            var selector = GetApplicationInstanceSelector(instanceRecords, string.Empty);

            selector.LoadInstance().InstanceName.Should().Be("instance 2");
        }

        [Test]
        public void LoadInstanceThrowsWhenInstanceNameNotFound()
        {
            var instanceRecords = new List<ApplicationInstanceRecord> { new ApplicationInstanceRecord("instance 2", "c:\\temp\\2.config") };
            var selector = GetApplicationInstanceSelector(instanceRecords, "instance 1");

            ((Action)(() => selector.LoadInstance()))
                .Should()
                .Throw<ControlledFailureException>()
                .WithMessage("Instance instance 1 of OctopusServer has not been configured on this machine. Available instances: instance 2.");
        }

        [Test]
        public void LoadInstanceMatchesInstanceNameCaseInsensitively()
        {
            var instanceRecords = new List<ApplicationInstanceRecord> { new ApplicationInstanceRecord("Instance 2", "c:\\temp\\2.config") };
            var selector = GetApplicationInstanceSelector(instanceRecords, "INSTANCE 2");

            selector.LoadInstance().InstanceName.Should().Be("Instance 2");
        }

        [Test]
        public void LoadInstanceMatchesInstanceNameCaseSensitivelyWhenOneOfThemIsAnExactMatch()
        {
            var applicationInstanceRecord = new ApplicationInstanceRecord("INSTANCE 2", "c:\\temp\\2b.config");

            var instanceRecords = new List<ApplicationInstanceRecord>
            {
                new ApplicationInstanceRecord("Instance 2", "c:\\temp\\2a.config"),
                applicationInstanceRecord
            };
            var selector = GetApplicationInstanceSelector(instanceRecords, "INSTANCE 2");

            selector.LoadInstance().InstanceName.Should().Be("INSTANCE 2");
        }

        [Test]
        public void LoadInstanceThrowsWhenMultipleCaseInsensitiveMatchesButNoExactMatch()
        {
            //note: this could only happen:
            // - once we've stopped using the registry as the instance store
            // - on a case sensitive operating system (ie linux or mac)
            // - where the instance configuration was done manually (as `create-instance` blocks it)

            var instanceRecords = new List<ApplicationInstanceRecord>
            {
                new ApplicationInstanceRecord("Instance 2", "c:\\temp\\2a.config"),
                new ApplicationInstanceRecord("INSTANCE 2", "c:\\temp\\2b.config")
            };
            var selector = GetApplicationInstanceSelector(instanceRecords, "instance 2");

            ((Action)(() => selector.LoadInstance()))
                .Should()
                .Throw<ControlledFailureException>()
                .WithMessage("Instance instance 2 of OctopusServer could not be matched to one of the existing instances: Instance 2, INSTANCE 2.");
        }

        static ApplicationInstanceSelector GetApplicationInstanceSelector(List<ApplicationInstanceRecord> instanceRecords, string currentInstanceName)
        {
            ((IApplicationConfigurationWithMultipleInstances)ConfigurationStore).ListInstances().Returns(instanceRecords);

            ((IApplicationConfigurationStrategy)ConfigurationStore).LoadedConfiguration(Arg.Any<ApplicationRecord>())
                .Returns(c =>
                {
                    var record = (ApplicationInstanceRecord)c.Args()[0];
                    return new XmlFileKeyValueStore(Substitute.For<IOctopusFileSystem>(), record.ConfigurationFilePath);
                });

            StartUpInstanceRequest startupRequest;
            if (string.IsNullOrWhiteSpace(currentInstanceName))
                startupRequest = new StartUpDynamicInstanceRequest(ApplicationName.OctopusServer);
            else
                startupRequest = new StartUpRegistryInstanceRequest(ApplicationName.OctopusServer, currentInstanceName);

            var selector = new ApplicationInstanceSelector(startupRequest,
                new[] { (IApplicationConfigurationStrategy)ConfigurationStore, OtherStrategy },
                Substitute.For<ILogFileOnlyLogger>());
            return selector;
        }
    }
}