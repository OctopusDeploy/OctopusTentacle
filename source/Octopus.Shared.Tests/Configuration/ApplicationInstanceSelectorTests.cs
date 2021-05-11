using System;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using Octopus.Configuration;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Configuration
{
    [TestFixture]
    public class ApplicationInstanceSelectorTests
    {
        IApplicationInstanceStore applicationInstanceStore = Substitute.For<IApplicationInstanceStore>();
        IOctopusFileSystem octopusFileSystem = Substitute.For<IOctopusFileSystem>();

        [SetUp]
        public void Setup()
        {
            applicationInstanceStore = Substitute.For<IApplicationInstanceStore>();
            octopusFileSystem = Substitute.For<IOctopusFileSystem>();
        }

        ApplicationInstanceSelector CreateApplicationInstanceSelector(StartUpInstanceRequest instanceRequest = null,
            IApplicationConfigurationContributor[] additionalConfigurations = null)
        {
            return new ApplicationInstanceSelector(ApplicationName.Tentacle,
                applicationInstanceStore,
                instanceRequest ?? new StartUpDynamicInstanceRequest(),
                additionalConfigurations ?? new IApplicationConfigurationContributor[0],
                octopusFileSystem,
                Substitute.For<ISystemLog>());
        }

        [Test]
        public void GivenNoInstanceAvailable_ThenCurrentThrows()
        {
            var mock = CreateApplicationInstanceSelector();
            SetupMissingStoredInstances();
            
            Assert.Throws<ControlledFailureException>(() => { var x = mock.Current;  });
        }
        
        [Test]
        public void GivenNoInstanceAvailable_ThenCanLoadCurrentInstanceIsFalse()
        {
            var mock = CreateApplicationInstanceSelector();
            SetupMissingStoredInstances();
                
            mock.CanLoadCurrentInstance().Should().BeFalse();
        }
        
        [Test]
        public void GivenNamedInstanceExists_WhenRequestingThatInstance_ThenCurrentReturnsThatInstance()
        {
            var name = Guid.NewGuid().ToString();
            var mock = CreateApplicationInstanceSelector(new StartUpRegistryInstanceRequest(name));
            var configPath = SetupAvailableStoredInstance(name);

            mock.Current.InstanceName.Should().Be(name);
            mock.Current.ConfigurationPath.Should().Be(configPath);
        }
        
        [Test]
        public void GivenMultipleNamedInstanceExists_WhenRequestingDefaultInstance_ThenCurrentReturnsDefaultInstance()
        {
            var mock = CreateApplicationInstanceSelector(new StartUpDynamicInstanceRequest());
             SetupAvailableStoredInstance("NAMED");
            var configPath = SetupAvailableStoredInstance(null);

            mock.Current.ConfigurationPath.Should().Be(configPath);
        }
        
        [Test]
        public void GivenConfigurationInstanceExists_WhenRequestingConfigurationInstance_ThenCurrentReturnsConfigurationInstance()
        {
            var filePath = "FILE.txt";
            var mock = CreateApplicationInstanceSelector(new StartUpConfigFileInstanceRequest(filePath));

            octopusFileSystem.FileExists(filePath).Returns(true);
            
            mock.Current.InstanceName.Should().BeNull();
            mock.Current.ConfigurationPath.Should().Be(filePath);
        }
        
        [Test]
        public void GivenConfigurationInstanceDoesNotExists_WhenRequestingConfigurationInstance_ThenThrows()
        {
            var filePath = "FILE.txt";
            var mock = CreateApplicationInstanceSelector(new StartUpConfigFileInstanceRequest(filePath));
            octopusFileSystem.FileExists(filePath).Returns(false);
            
            Assert.Throws<ControlledFailureException>(() => { var x = mock.Current;  });
        }

        [Test]
        public void GivenCWDConfigExists_WhenRequestingDynamicInstance_ThenCurrentReturnsCWDDefaultConfig()
        {
            var filePath = "FULLPATH";
            var defaultConfig = $"{ApplicationName.Tentacle}.config";
            var mock = CreateApplicationInstanceSelector(new StartUpDynamicInstanceRequest());
            octopusFileSystem.GetFullPath(defaultConfig).Returns(filePath);
            octopusFileSystem.FileExists(filePath).Returns(true);
            
            mock.Current.InstanceName.Should().BeNull();
            mock.Current.ConfigurationPath.Should().Be("FULLPATH");
        }
        
        
        [Test]
        public void GivenCWDConfigAndDefaultInstanceExists_WhenRequestingDynamicInstance_ThenCurrentReturnsCWDDefaultConfig()
        {
            var filePath = "FULLPATH";
            var defaultConfig = $"{ApplicationName.Tentacle}.config";
            var mock = CreateApplicationInstanceSelector(new StartUpDynamicInstanceRequest());
            SetupAvailableStoredInstance(null);
            
            octopusFileSystem.GetFullPath(defaultConfig).Returns(filePath);
            octopusFileSystem.FileExists(filePath).Returns(true);
            
            
            mock.Current.InstanceName.Should().BeNull();
            mock.Current.ConfigurationPath.Should().Be("FULLPATH");
        }
        
        [Test]
        public void GivenContributingVariableSourceExists_ThenOverridingVariableIsAvailableInConfiguration()
        {
            var mockContributor = Substitute.For<IApplicationConfigurationContributor>();
            var mockKeyValueStore = Substitute.For<IAggregatableKeyValueStore>();
            mockKeyValueStore.TryGet<string>("FOO", Arg.Any<ProtectionLevel>()).Returns(info => (true, "BAR"));
            mockContributor.LoadContributedConfiguration().Returns(mockKeyValueStore);
                
            var mock = CreateApplicationInstanceSelector(additionalConfigurations: new []  { mockContributor });
            SetupAvailableStoredInstance(null);
            
            mock.Current.Configuration.Get<string>("FOO").Should().Be("BAR");
        }
        
        [Test]
        public void GivenMultipleContributingVariableSources_ThenPrioritySetsRetrievalOrder()
        {
            var contributor1 = SetupMockContributor(1,"FOO", "PRIORITY_1");
            var contributor2 = SetupMockContributor(2,"FOO", "PRIORITY_2");
            var contributor3 = SetupMockContributor(3,"FOO", "PRIORITY_3");
            var unorderedContributors = new[] { contributor2, contributor1, contributor3 };
            
            var mock = CreateApplicationInstanceSelector(additionalConfigurations: unorderedContributors);
            SetupAvailableStoredInstance(null);
            
            mock.Current.Configuration.Get<string>("FOO").Should().Be("PRIORITY_1");
        }

        static IApplicationConfigurationContributor SetupMockContributor(int priority, string entryKey, string entryValue)
        {
            var mockContributor = Substitute.For<IApplicationConfigurationContributor>();
            var mockKeyValueStore = Substitute.For<IAggregatableKeyValueStore>();
            mockKeyValueStore.TryGet<string>(entryKey, Arg.Any<ProtectionLevel>()).Returns(info => (true, entryValue));
            mockContributor.Priority.Returns(priority);
            mockContributor.LoadContributedConfiguration().Returns(mockKeyValueStore);
            return mockContributor;
        }

        void SetupMissingStoredInstances(string instanceName = null)
        {
            applicationInstanceStore.LoadInstanceDetails(instanceName).Throws(new ControlledFailureException(""));
        }

        string SetupAvailableStoredInstance(string instanceName)
        {
            var configPath = Guid.NewGuid().ToString();
            applicationInstanceStore.LoadInstanceDetails(instanceName).Returns(new ApplicationInstanceRecord(instanceName, configPath));
            return configPath;
        }

        /*
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

            ((Action)(() =>
                {
                    var x = selector.Current;
                }))
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
        }*/
    }
}