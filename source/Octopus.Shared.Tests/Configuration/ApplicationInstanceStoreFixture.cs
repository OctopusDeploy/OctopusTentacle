using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Configuration
{
    [TestFixture]
    public class ApplicationInstanceStoreFixture
    {
        private IRegistryApplicationInstanceStore registryStore;
        private IOctopusFileSystem fileSystem;
        private ApplicationInstanceStore instanceStore;

        [SetUp]
        public void Setup()
        {
            registryStore = Substitute.For<IRegistryApplicationInstanceStore>();
            fileSystem = Substitute.For<IOctopusFileSystem>();
            ILog log = Substitute.For<ILog>();
            instanceStore = new ApplicationInstanceStore(log, fileSystem, registryStore);
        }

        [Test]
        public void ListInstance_NoRegistryEntries_ShouldListFromFileSystem()
        {
            registryStore.GetListFromRegistry(Arg.Any<ApplicationName>()).Returns(Enumerable.Empty<ApplicationInstanceRecord>());
            fileSystem.DirectoryExists(Arg.Any<string>()).Returns(true);
            fileSystem.EnumerateFiles(Arg.Any<string>()).Returns(new List<string> {"file1", "file2"});
            fileSystem.FileExists(Arg.Any<string>()).Returns(true);
            fileSystem.ReadFile(Arg.Is("file1")).Returns("{\"Name\": \"instance1\",\"ConfigurationFilePath\": \"configFilePath1\"}");
            fileSystem.ReadFile(Arg.Is("file2")).Returns("{\"Name\": \"instance2\",\"ConfigurationFilePath\": \"configFilePath2\"}");

            var instances = instanceStore.ListInstances(ApplicationName.OctopusServer);
            instances.Should().BeEquivalentTo(new List<ApplicationInstanceRecord>
            {
                new ApplicationInstanceRecord("instance1", ApplicationName.OctopusServer, "configFilePath1"),
                new ApplicationInstanceRecord("instance2", ApplicationName.OctopusServer, "configFilePath2")
            });
        }

        [Test]
        public void ListInstance_NoFileSystem_ShouldListRegistry()
        {
            registryStore.GetListFromRegistry(Arg.Any<ApplicationName>()).Returns(new List<ApplicationInstanceRecord>
            {
                new ApplicationInstanceRecord("instance1", ApplicationName.OctopusServer, "configFilePath1"),
                new ApplicationInstanceRecord("instance2", ApplicationName.OctopusServer, "configFilePath2")
            });
            fileSystem.DirectoryExists(Arg.Any<string>()).Returns(false);

            var instances = instanceStore.ListInstances(ApplicationName.OctopusServer);
            instances.Should().BeEquivalentTo(new List<ApplicationInstanceRecord>
            {
                new ApplicationInstanceRecord("instance1", ApplicationName.OctopusServer, "configFilePath1"),
                new ApplicationInstanceRecord("instance2", ApplicationName.OctopusServer, "configFilePath2")
            });
        }

        [Test]
        public void ListInstance_ShouldPreferFileSystemEntries()
        {
            registryStore.GetListFromRegistry(Arg.Any<ApplicationName>()).Returns(new List<ApplicationInstanceRecord>
            {
                new ApplicationInstanceRecord("instance1", ApplicationName.OctopusServer, "registryFilePath1"),
                new ApplicationInstanceRecord("instance2", ApplicationName.OctopusServer, "registryFilePath2")
            });
            fileSystem.DirectoryExists(Arg.Any<string>()).Returns(true);
            fileSystem.EnumerateFiles(Arg.Any<string>()).Returns(new List<string> { "file1", "file2" });
            fileSystem.FileExists(Arg.Any<string>()).Returns(true);
            fileSystem.ReadFile(Arg.Is("file1")).Returns("{\"Name\": \"instance2\",\"ConfigurationFilePath\": \"fileConfigFilePath2\"}");
            fileSystem.ReadFile(Arg.Is("file2")).Returns("{\"Name\": \"instance3\",\"ConfigurationFilePath\": \"fileConfigFilePath3\"}");

            var instances = instanceStore.ListInstances(ApplicationName.OctopusServer);
            instances.Should().BeEquivalentTo(new List<ApplicationInstanceRecord>
            {
                new ApplicationInstanceRecord("instance1", ApplicationName.OctopusServer, "registryFilePath1"),
                new ApplicationInstanceRecord("instance2", ApplicationName.OctopusServer, "fileConfigFilePath2"),
                new ApplicationInstanceRecord("instance3", ApplicationName.OctopusServer, "fileConfigFilePath3")
            });
        }

        [Test]
        public void GetInstance_ShouldPreferFileSystemEntries()
        {
            registryStore.GetListFromRegistry(Arg.Any<ApplicationName>()).Returns(new List<ApplicationInstanceRecord>
            {
                new ApplicationInstanceRecord("instance1", ApplicationName.OctopusServer, "registryFilePath1"),
            });
            fileSystem.DirectoryExists(Arg.Any<string>()).Returns(true);
            fileSystem.EnumerateFiles(Arg.Any<string>()).Returns(new List<string> { "file1" });
            fileSystem.FileExists(Arg.Any<string>()).Returns(true);
            fileSystem.ReadFile(Arg.Is("file1")).Returns("{\"Name\": \"instance1\",\"ConfigurationFilePath\": \"fileConfigFilePath2\"}");

            var instance = instanceStore.GetInstance(ApplicationName.OctopusServer, "instance1");
            instance.InstanceName.Should().Be("instance1");
            instance.ConfigurationFilePath.Should().Be("fileConfigFilePath2");
        }

        [Test]
        public void GetInstance_ShouldFilterCorrectInstance()
        {
            registryStore.GetListFromRegistry(ApplicationName.Tentacle).Returns(new List<ApplicationInstanceRecord>
            {
                new ApplicationInstanceRecord("instance1", ApplicationName.Tentacle, "TentaclePath"),
            });
            registryStore.GetListFromRegistry(ApplicationName.OctopusServer).Returns(new List<ApplicationInstanceRecord>
            {
                new ApplicationInstanceRecord("instance1", ApplicationName.OctopusServer, "ServerPath1"),
                new ApplicationInstanceRecord("instance2", ApplicationName.OctopusServer, "ServerPath2")
            });

            var instance = instanceStore.GetInstance(ApplicationName.OctopusServer, "instance1");
            instance.InstanceName.Should().Be("instance1");
            instance.ApplicationName.Should().Be(ApplicationName.OctopusServer);
        }
        
        [Test]
        public void GetInstance_ShouldReturnNullIfNoneFound()
        {
            registryStore.GetListFromRegistry(ApplicationName.OctopusServer).Returns(new List<ApplicationInstanceRecord>
            {
                new ApplicationInstanceRecord("instance1", ApplicationName.OctopusServer, "ServerPath1"),
                new ApplicationInstanceRecord("instance2", ApplicationName.OctopusServer, "ServerPath2")
            });

            var instance = instanceStore.GetInstance(ApplicationName.OctopusServer, "I AM FAKE");
            Assert.IsNull(instance);
        }

        [Test]
        public void MigrateInstance()
        {
            var sourceInstance = new ApplicationInstanceRecord("instance1", ApplicationName.OctopusServer, "configFilePath");
            registryStore.GetInstanceFromRegistry(Arg.Is<ApplicationName>(ApplicationName.OctopusServer), Arg.Is<string>("instance1")).Returns(sourceInstance);

            instanceStore.MigrateInstance(sourceInstance);
            fileSystem.Received().CreateDirectory(Arg.Any<string>());
            fileSystem.Received().OverwriteFile(Arg.Is<string>(x => x.Contains(sourceInstance.InstanceName)), Arg.Is<string>(x => x.Contains(sourceInstance.ConfigurationFilePath)));

        }
    }
}
