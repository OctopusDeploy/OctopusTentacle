using System;
using System.Collections.Generic;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Configuration;
using Octopus.Shared.Configuration;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Startup;

namespace Octopus.Shared.Tests.Configuration
{
    [TestFixture]
    public class ApplicationInstanceSelectorTests
    {
        static IApplicationInstanceStrategy instanceStore;

        [Test]
        public void LoadInstanceThrowsWhenNoInstanceNamePassedAndNoInstancesConfigured()
        {
            var selector = GetApplicationInstanceSelector(new List<ApplicationInstanceRecord>(), string.Empty);
            instanceStore.AnyInstancesConfigured().Returns(false);
            ((Action)(() => selector.LoadInstance()))
                .Should().Throw<ControlledFailureException>()
                .WithMessage("There are no instances of OctopusServer configured on this machine. Please run the setup wizard, configure an instance using the command-line interface, specify a configuration file, or set the required environment variables.");
        }

        [Test]
        public void LoadInstanceThrowsWhenNoInstanceNameIsPassedAndMoreThanOneInstanceWithNoDefaultInstance()
        {
            var instanceRecords = new List<ApplicationInstanceRecord>
            {
                new PersistedApplicationInstanceRecord("My instance", "c:\\temp\\1.config", false),
                new PersistedApplicationInstanceRecord("instance 2", "c:\\temp\\2.config", false)
            };
            var selector = GetApplicationInstanceSelector(instanceRecords, string.Empty);
            ((Action)(() => selector.LoadInstance()))
                .Should().Throw<ControlledFailureException>()
                .WithMessage("There is more than one instance of OctopusServer configured on this machine. Please pass --instance=INSTANCENAME when invoking this command to target a specific instance. Available instances: My instance, instance 2.");
        }

        [Test]
        public void LoadInstanceReturnsDefaultInstanceWhenNoInstanceNameIsPassedAndThereAreMultipleInstancesAndOneIsTheDefaultInstance()
        {
            var defaultName = PersistedApplicationInstanceRecord.GetDefaultInstance(ApplicationName.OctopusServer);
            var instanceRecords = new List<ApplicationInstanceRecord>
            {
                new PersistedApplicationInstanceRecord(PersistedApplicationInstanceRecord.GetDefaultInstance(ApplicationName.OctopusServer), "c:\\temp\\0.config", true),
                new PersistedApplicationInstanceRecord("My instance", "c:\\temp\\1.config", false),
                new PersistedApplicationInstanceRecord("instance 2", "c:\\temp\\2.config", false)
            };
            var selector = GetApplicationInstanceSelector(instanceRecords, string.Empty);
            selector.LoadInstance().InstanceName.Should().Be(defaultName);
        }

        [Test]
        public void LoadInstanceReturnsOnlyInstanceWhenNoInstanceNameIsPassedAndOnlyOneInstance()
        {
            var instanceRecords = new List<ApplicationInstanceRecord> { new PersistedApplicationInstanceRecord("instance 2", "c:\\temp\\2.config", false) };
            var selector = GetApplicationInstanceSelector(instanceRecords, string.Empty);

            selector.LoadInstance().InstanceName.Should().Be("instance 2");
        }

        [Test]
        public void LoadInstanceThrowsWhenInstanceNameNotFound()
        {
            var instanceRecords = new List<ApplicationInstanceRecord> { new PersistedApplicationInstanceRecord("instance 2", "c:\\temp\\2.config", false) };
            var selector = GetApplicationInstanceSelector(instanceRecords, "instance 1");

            ((Action)(() => selector.LoadInstance()))
                .Should().Throw<ControlledFailureException>()
                .WithMessage("Instance instance 1 of OctopusServer has not been configured on this machine. Available instances: instance 2.");
        }

        [Test]
        public void LoadInstanceMatchesInstanceNameCaseInsensitively()
        {
            var instanceRecords = new List<ApplicationInstanceRecord> { new PersistedApplicationInstanceRecord("Instance 2", "c:\\temp\\2.config", false) };
            var selector = GetApplicationInstanceSelector(instanceRecords, "INSTANCE 2");

            selector.LoadInstance().InstanceName.Should().Be("Instance 2");
        }

        [Test]
        public void LoadInstanceMatchesInstanceNameCaseSensitivelyWhenOneOfThemIsAnExactMatch()
        {
            var applicationInstanceRecord = new PersistedApplicationInstanceRecord("INSTANCE 2", "c:\\temp\\2b.config", false);

            var instanceRecords = new List<ApplicationInstanceRecord>
            {
                new PersistedApplicationInstanceRecord("Instance 2", "c:\\temp\\2a.config", false),
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
                new PersistedApplicationInstanceRecord("Instance 2", "c:\\temp\\2a.config", false),
                new PersistedApplicationInstanceRecord("INSTANCE 2", "c:\\temp\\2b.config", false)
            };
            var selector = GetApplicationInstanceSelector(instanceRecords, "instance 2");

            ((Action)(() => selector.LoadInstance()))
                .Should().Throw<ControlledFailureException>()
                .WithMessage("Instance instance 2 of OctopusServer could not be matched to one of the existing instances: Instance 2, INSTANCE 2.");
        }

        static ApplicationInstanceSelector GetApplicationInstanceSelector(List<ApplicationInstanceRecord> instanceRecords, string currentInstanceName)
        {
            instanceStore = Substitute.For<IApplicationInstanceStrategy>();
            instanceStore.ListInstances().Returns(instanceRecords);
            instanceStore.AnyInstancesConfigured().Returns(true);

            var keyValueStore = Substitute.For<IModifiableKeyValueStore>();

            instanceStore.LoadedApplicationInstance(Arg.Any<ApplicationInstanceRecord>())
                .Returns(c =>
                {
                    var record = (PersistedApplicationInstanceRecord)c.Args()[0];
                    return new LoadedPersistedApplicationInstance(record.InstanceName, keyValueStore, record.ConfigurationFilePath);
                });

            StartUpInstanceRequest startupRequest;
            if (string.IsNullOrWhiteSpace(currentInstanceName))
                startupRequest = new StartUpDynamicInstanceRequest(ApplicationName.OctopusServer);
            else
                startupRequest = new StartUpPersistedInstanceRequest(ApplicationName.OctopusServer, currentInstanceName);
            
            var selector = new ApplicationInstanceSelector(startupRequest, 
                new [] { instanceStore },
                Substitute.For<ILogFileOnlyLogger>());
            return selector;
        }
    }
}