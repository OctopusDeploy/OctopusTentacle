using System;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using Octopus.Configuration;
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Configuration
{
    [TestFixture]
    public class ApplicationInstanceSelectorFixture
    {
        private IApplicationInstanceStore applicationInstanceStore = Substitute.For<IApplicationInstanceStore>();
        private IOctopusFileSystem octopusFileSystem = Substitute.For<IOctopusFileSystem>();

        [SetUp]
        public void Setup()
        {
            applicationInstanceStore = Substitute.For<IApplicationInstanceStore>();
            octopusFileSystem = Substitute.For<IOctopusFileSystem>();
        }

        [Test]
        public void GivenNoInstanceAvailable_ThenCurrentThrows()
        {
            var mock = CreateApplicationInstanceSelector();
            SetupMissingStoredInstances();

            Assert.Throws<ControlledFailureException>(() =>
            {
                var x = mock.Current;
            });
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
        public void GivenNamedInstanceExists_WhenConfigurationIsMissing_ThenCurrentThrowsException()
        {
            var name = Guid.NewGuid().ToString();
            var mock = CreateApplicationInstanceSelector(new StartUpRegistryInstanceRequest(name));
            var configPath = SetupAvailableStoredInstance(name);
            octopusFileSystem.FileExists(configPath).Returns(false);

            Assert.Throws<ControlledFailureException>(() =>
            {
                var _ = mock.Current.InstanceName;
            });
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

            Assert.Throws<ControlledFailureException>(() =>
            {
                var x = mock.Current;
            });
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

            var mock = CreateApplicationInstanceSelector(additionalConfigurations: new[] { mockContributor });
            SetupAvailableStoredInstance(null);

            mock.Current.Configuration.Get<string>("FOO").Should().Be("BAR");
        }

        [Test]
        public void GivenMultipleContributingVariableSources_ThenPrioritySetsRetrievalOrder()
        {
            var contributor1 = SetupMockContributor(1, "FOO", "PRIORITY_1");
            var contributor2 = SetupMockContributor(2, "FOO", "PRIORITY_2");
            var contributor3 = SetupMockContributor(3, "FOO", "PRIORITY_3");
            var unorderedContributors = new[] { contributor2, contributor1, contributor3 };

            var mock = CreateApplicationInstanceSelector(additionalConfigurations: unorderedContributors);
            SetupAvailableStoredInstance(null);

            mock.Current.Configuration.Get<string>("FOO").Should().Be("PRIORITY_1");
        }

        private static IApplicationConfigurationContributor SetupMockContributor(int priority, string entryKey, string entryValue)
        {
            var mockContributor = Substitute.For<IApplicationConfigurationContributor>();
            var mockKeyValueStore = Substitute.For<IAggregatableKeyValueStore>();
            mockKeyValueStore.TryGet<string>(entryKey, Arg.Any<ProtectionLevel>()).Returns(info => (true, entryValue));
            mockContributor.Priority.Returns(priority);
            mockContributor.LoadContributedConfiguration().Returns(mockKeyValueStore);
            return mockContributor;
        }

        private void SetupMissingStoredInstances(string? instanceName = null)
        {
            applicationInstanceStore.LoadInstanceDetails(instanceName).Throws(new ControlledFailureException(""));
            applicationInstanceStore.TryLoadInstanceDetails(instanceName, out Arg.Any<ApplicationInstanceRecord>()!)
                .Returns(x => false);
        }

        private string SetupAvailableStoredInstance(string instanceName)
        {
            var configPath = Guid.NewGuid().ToString();
            var record = new ApplicationInstanceRecord(instanceName, configPath);
            applicationInstanceStore.LoadInstanceDetails(instanceName).Returns(record);
            applicationInstanceStore.TryLoadInstanceDetails(instanceName, out Arg.Any<ApplicationInstanceRecord>()!)
                .Returns(x =>
                {
                    x[1] = record;
                    return true;
                });
            octopusFileSystem.FileExists(configPath).Returns(true);
            return configPath;
        }

        private ApplicationInstanceSelector CreateApplicationInstanceSelector(StartUpInstanceRequest? instanceRequest = null,
            IApplicationConfigurationContributor[]? additionalConfigurations = null)
        {
            return new ApplicationInstanceSelector(ApplicationName.Tentacle,
                applicationInstanceStore,
                instanceRequest ?? new StartUpDynamicInstanceRequest(),
                additionalConfigurations ?? new IApplicationConfigurationContributor[0],
                octopusFileSystem,
                Substitute.For<ISystemLog>());
        }
    }
}