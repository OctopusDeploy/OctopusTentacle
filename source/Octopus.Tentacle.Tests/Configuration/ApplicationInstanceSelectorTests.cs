using System;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Kubernetes.Configuration;
using Octopus.Tentacle.Kubernetes.Crypto;
using Octopus.Tentacle.Tests.Support.TestAttributes;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Configuration
{
    public class ApplicationInstanceSelectorTests
    {
        [Test]
        public void GivenNoInstanceAvailable_ThenCurrentThrows()
        {
            var arrange = new Arrangement();
            var mock = arrange.CreateSelector();
            arrange.SetupMissingStoredInstances();
            
            Assert.Throws<ControlledFailureException>(() => { var x = mock.Current;  });
        }
        
        [Test]
        public void GivenNoInstanceAvailable_ThenCanLoadCurrentInstanceIsFalse()
        {
            var arrange = new Arrangement();
            var mock = arrange.CreateSelector();
            arrange.SetupMissingStoredInstances();
                
            mock.CanLoadCurrentInstance().Should().BeFalse();
        }
        
        [Test]
        public void GivenNamedInstanceExists_WhenRequestingThatInstance_ThenCurrentReturnsThatInstance()
        {
            var arrange = new Arrangement();
            var name = Guid.NewGuid().ToString();
            var mock = arrange.CreateSelector(new StartUpRegistryInstanceRequest(name));
            var configPath = arrange.SetupAvailableStoredInstance(name);

            mock.Current.InstanceName.Should().Be(name);
            mock.Current.ConfigurationPath.Should().Be(configPath);
        }

        [Test]
        public void GivenNamedInstanceExists_WhenConfigurationIsMissing_ThenCurrentThrowsException()
        {
            var arrange = new Arrangement();
            var name = Guid.NewGuid().ToString();
            var mock = arrange.CreateSelector(new StartUpRegistryInstanceRequest(name));
            var configPath = arrange.SetupAvailableStoredInstance(name);
            arrange.FileSystem.FileExists(configPath).Returns(false);

            Assert.Throws<ControlledFailureException>(() =>
            {
                var _ = mock.Current.InstanceName;
            });
        }

        [Test]
        public void GivenMultipleNamedInstanceExists_WhenRequestingDefaultInstance_ThenCurrentReturnsDefaultInstance()
        {
            var arrange = new Arrangement();
            var mock = arrange.CreateSelector(new StartUpDynamicInstanceRequest());
             arrange.SetupAvailableStoredInstance("NAMED");
            var configPath = arrange.SetupAvailableStoredInstance(null);

            mock.Current.ConfigurationPath.Should().Be(configPath);
        }
        
        [Test]
        public void GivenConfigurationInstanceExists_WhenRequestingConfigurationInstance_ThenCurrentReturnsConfigurationInstance()
        {
            var arrange = new Arrangement();
            var filePath = "FILE.txt";
            var mock = arrange.CreateSelector(new StartUpConfigFileInstanceRequest(filePath));

            arrange.FileSystem.FileExists(filePath).Returns(true);
            
            mock.Current.InstanceName.Should().BeNull();
            mock.Current.ConfigurationPath.Should().Be(filePath);
        }
        
        [Test]
        [LinuxOrMacTest]
        public void GivenARegisteredInstanceUsesTheConfiguration_WhenRequestingConfigurationInstance_ThenItUsesThatInstancesKey()
        {
            var arrange = new Arrangement();
            var filePath = "/etc/octopus/tentacle.config";
            var mock = arrange.CreateSelector(new StartUpConfigFileInstanceRequest(filePath));
            arrange.FileSystem.FileExists(filePath).Returns(true);
            arrange.FileSystem.GetFullPath(Arg.Any<string>()).Returns(c => c.Arg<string>());
            arrange.Store.ListInstances().Returns(new[]
            {
                new ApplicationInstanceRecord("Other", "/etc/octopus/other.config"),
                new ApplicationInstanceRecord("Registered", filePath)
            });

            mock.Current.KeyFilePath.Should().Be(ConfigurationEncryptionKey.KeyFilePathFor("Registered"));
        }

        [Test]
        public void GivenConfigurationInstanceDoesNotExists_WhenRequestingConfigurationInstance_ThenThrows()
        {
            var arrange = new Arrangement();
            var filePath = "FILE.txt";
            var mock = arrange.CreateSelector(new StartUpConfigFileInstanceRequest(filePath));
            arrange.FileSystem.FileExists(filePath).Returns(false);
            
            Assert.Throws<ControlledFailureException>(() => { var x = mock.Current;  });
        }

        [Test]
        public void GivenCWDConfigExists_WhenRequestingDynamicInstance_ThenCurrentReturnsCWDDefaultConfig()
        {
            var arrange = new Arrangement();
            var filePath = "FULLPATH";
            var defaultConfig = $"{ApplicationName.Tentacle}.config";
            var mock = arrange.CreateSelector(new StartUpDynamicInstanceRequest());
            arrange.FileSystem.GetFullPath(defaultConfig).Returns(filePath);
            arrange.FileSystem.FileExists(filePath).Returns(true);
            
            mock.Current.InstanceName.Should().BeNull();
            mock.Current.ConfigurationPath.Should().Be("FULLPATH");
        }
        
        
        [Test]
        public void GivenCWDConfigAndDefaultInstanceExists_WhenRequestingDynamicInstance_ThenCurrentReturnsCWDDefaultConfig()
        {
            var arrange = new Arrangement();
            var filePath = "FULLPATH";
            var defaultConfig = $"{ApplicationName.Tentacle}.config";
            var mock = arrange.CreateSelector(new StartUpDynamicInstanceRequest());
            arrange.SetupAvailableStoredInstance(null);
            
            arrange.FileSystem.GetFullPath(defaultConfig).Returns(filePath);
            arrange.FileSystem.FileExists(filePath).Returns(true);
            
            
            mock.Current.InstanceName.Should().BeNull();
            mock.Current.ConfigurationPath.Should().Be("FULLPATH");
        }
        
        [Test]
        public void GivenContributingVariableSourceExists_ThenOverridingVariableIsAvailableInConfiguration()
        {
            var arrange = new Arrangement();
            var mockContributor = Substitute.For<IApplicationConfigurationContributor>();
            var mockKeyValueStore = Substitute.For<IAggregatableKeyValueStore>();
            mockKeyValueStore.TryGet<string>("FOO", Arg.Any<ProtectionLevel>()).Returns(info => (true, "BAR"));
            mockContributor.LoadContributedConfiguration().Returns(mockKeyValueStore);
                
            var mock = arrange.CreateSelector(additionalConfigurations: new []  { mockContributor });
            arrange.SetupAvailableStoredInstance(null);
            
            mock.Current.Configuration.Get<string>("FOO").Should().Be("BAR");
        }
        
        [Test]
        public void GivenMultipleContributingVariableSources_ThenPrioritySetsRetrievalOrder()
        {
            var arrange = new Arrangement();
            var contributor1 = SetupMockContributor(1,"FOO", "PRIORITY_1");
            var contributor2 = SetupMockContributor(2,"FOO", "PRIORITY_2");
            var contributor3 = SetupMockContributor(3,"FOO", "PRIORITY_3");
            var unorderedContributors = new[] { contributor2, contributor1, contributor3 };
            
            var mock = arrange.CreateSelector(additionalConfigurations: unorderedContributors);
            arrange.SetupAvailableStoredInstance(null);
            
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

        class Arrangement
        {
            public IApplicationInstanceStore Store { get; } = Substitute.For<IApplicationInstanceStore>();
            public IOctopusFileSystem FileSystem { get; } = Substitute.For<IOctopusFileSystem>();

            public void SetupMissingStoredInstances(string? instanceName = null)
            {
                Store.LoadInstanceDetails(instanceName).Throws(new ControlledFailureException(""));
            }

            public string SetupAvailableStoredInstance(string instanceName)
            {
                var configPath = Guid.NewGuid().ToString();
                var record = new ApplicationInstanceRecord(instanceName, configPath);
                Store.LoadInstanceDetails(instanceName).Returns(record);
                FileSystem.FileExists(configPath).Returns(true);
                return configPath;
            }

            public ApplicationInstanceSelector CreateSelector(StartUpInstanceRequest? instanceRequest = null,
                IApplicationConfigurationContributor[]? additionalConfigurations = null)
            {
                var log = Substitute.For<ISystemLog>();
                return new ApplicationInstanceSelector(ApplicationName.Tentacle,
                    Store,
                    instanceRequest ?? new StartUpDynamicInstanceRequest(),
                    additionalConfigurations ?? new IApplicationConfigurationContributor[0],
                    new Lazy<ConfigMapKeyValueStore>(() => new ConfigMapKeyValueStore(Substitute.For<IKubernetesConfigMapService>(), Substitute.For<IKubernetesMachineKeyEncryptor>())),
                    FileSystem,
                    log);
            }
        }
    }
}
