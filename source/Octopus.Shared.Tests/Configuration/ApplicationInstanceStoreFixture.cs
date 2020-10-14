using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Configuration
{
    [TestFixture]
    public class ApplicationInstanceStoreFixture
    {
        private IRegistryApplicationInstanceStore registryStore;
        private IOctopusFileSystem fileSystem;
        private PersistedApplicationConfigurationStore configurationStore;

        [SetUp]
        public void Setup()
        {
            registryStore = Substitute.For<IRegistryApplicationInstanceStore>();
            fileSystem = Substitute.For<IOctopusFileSystem>();
            ILog log = Substitute.For<ILog>();
            configurationStore = new PersistedApplicationConfigurationStore(new StartUpPersistedInstanceRequest(ApplicationName.OctopusServer, "instance 1"),  log, fileSystem, registryStore);
        }

        [Test]
        public void ListInstance_NoRegistryEntries_ShouldListFromFileSystem()
        {
            registryStore.GetListFromRegistry().Returns(Enumerable.Empty<PersistedApplicationInstanceRecord>());
            fileSystem.DirectoryExists(Arg.Any<string>()).Returns(true);
            fileSystem.EnumerateFiles(Arg.Any<string>()).Returns(new List<string> {"file1", "file2"});
            fileSystem.FileExists(Arg.Any<string>()).Returns(true);
            fileSystem.ReadFile(Arg.Is("file1")).Returns("{\"Name\": \"instance1\",\"ConfigurationFilePath\": \"configFilePath1\"}");
            fileSystem.ReadFile(Arg.Is("file2")).Returns("{\"Name\": \"instance2\",\"ConfigurationFilePath\": \"configFilePath2\"}");

            var instances = configurationStore.ListInstances();
            instances.Should().BeEquivalentTo(new List<PersistedApplicationInstanceRecord>
            {
                new PersistedApplicationInstanceRecord("instance1", "configFilePath1", false),
                new PersistedApplicationInstanceRecord("instance2", "configFilePath2", false)
            });
        }

        [Test]
        public void ListInstance_NoFileSystem_ShouldListRegistry()
        {
            registryStore.GetListFromRegistry().Returns(new List<PersistedApplicationInstanceRecord>
            {
                new PersistedApplicationInstanceRecord("instance1", "configFilePath1", false),
                new PersistedApplicationInstanceRecord("instance2", "configFilePath2", false)
            });
            fileSystem.DirectoryExists(Arg.Any<string>()).Returns(false);

            var instances = configurationStore.ListInstances();
            instances.Should().BeEquivalentTo(new List<PersistedApplicationInstanceRecord>
            {
                new PersistedApplicationInstanceRecord("instance1", "configFilePath1", false),
                new PersistedApplicationInstanceRecord("instance2", "configFilePath2", false)
            });
        }

        [Test]
        public void ListInstance_ShouldPreferFileSystemEntries()
        {
            registryStore.GetListFromRegistry().Returns(new List<PersistedApplicationInstanceRecord>
            {
                new PersistedApplicationInstanceRecord("instance1", "registryFilePath1", false),
                new PersistedApplicationInstanceRecord("instance2", "registryFilePath2", false)
            });
            fileSystem.DirectoryExists(Arg.Any<string>()).Returns(true);
            fileSystem.EnumerateFiles(Arg.Any<string>()).Returns(new List<string> { "file1", "file2" });
            fileSystem.FileExists(Arg.Any<string>()).Returns(true);
            fileSystem.ReadFile(Arg.Is("file1")).Returns("{\"Name\": \"instance2\",\"ConfigurationFilePath\": \"fileConfigFilePath2\"}");
            fileSystem.ReadFile(Arg.Is("file2")).Returns("{\"Name\": \"instance3\",\"ConfigurationFilePath\": \"fileConfigFilePath3\"}");

            var instances = configurationStore.ListInstances();
            instances.Should().BeEquivalentTo(new List<PersistedApplicationInstanceRecord>
            {
                new PersistedApplicationInstanceRecord("instance1", "registryFilePath1", false),
                new PersistedApplicationInstanceRecord("instance2", "fileConfigFilePath2", false),
                new PersistedApplicationInstanceRecord("instance3", "fileConfigFilePath3", false)
            });
        }

        [Test]
        public void GetInstance_ShouldPreferFileSystemEntries()
        {
            var configFilename = Path.Combine(configurationStore.InstancesFolder(), "instance-1.config");

            fileSystem.DirectoryExists(Arg.Any<string>()).Returns(true);
            fileSystem.FileExists(Arg.Any<string>()).Returns(true);
            fileSystem.ReadFile(Arg.Is(configFilename)).Returns("{\"Name\": \"instance 1\",\"ConfigurationFilePath\": \"fileConfigFilePath2\"}");

            var instance = configurationStore.GetInstance("instance 1");
            instance.InstanceName.Should().Be("instance 1");
            instance.ConfigurationFilePath.Should().Be("fileConfigFilePath2");
        }

        [Test]
        public void GetInstance_ShouldReturnNullIfNoneFound()
        {
            registryStore.GetListFromRegistry().Returns(new List<PersistedApplicationInstanceRecord>
            {
                new PersistedApplicationInstanceRecord("instance1", "ServerPath1", false),
                new PersistedApplicationInstanceRecord("instance2", "ServerPath2", false)
            });

            var instance = configurationStore.GetInstance("I AM FAKE");
            Assert.IsNull(instance);
        }

        [Test]
        public void MigrateInstance()
        {
            var sourceInstance = new PersistedApplicationInstanceRecord("instance1", "configFilePath", false);
            registryStore.GetInstanceFromRegistry(Arg.Is("instance1")).Returns(sourceInstance);

            configurationStore.MigrateInstance(sourceInstance);
            fileSystem.Received().CreateDirectory(Arg.Any<string>());
            fileSystem.Received().OverwriteFile(Arg.Is<string>(x => x.Contains(sourceInstance.InstanceName)), Arg.Is<string>(x => x.Contains(sourceInstance.ConfigurationFilePath)));

        }
    }
}
