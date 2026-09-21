using System;
using System.IO;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Core.Util;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Configuration.Crypto
{
    [TestFixture]
    public class LinuxGeneratedMachineKeyFixture
    {
        const string KeyFilePath = "/some/where/machinekey";
        const string ValidKeyFileContents = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=.ZmVkY2JhOTg3NjU0MzIxMA==";

        IOctopusFileSystem fileSystem;
        ISystemLog log;
        LinuxGeneratedMachineKey keySource;

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<IOctopusFileSystem>();
            log = Substitute.For<ISystemLog>();
            keySource = new LinuxGeneratedMachineKey(log, fileSystem, KeyFilePath);
        }

        [Test]
        public void GivenNoKeyFile_ThenGeneratesOneThatOnlyTheOwnerCanRead()
        {
            string written = null;
            fileSystem.FileExists(KeyFilePath).Returns(false);
            fileSystem.When(x => x.WriteAllTextOwnerOnly(KeyFilePath, Arg.Any<string>())).Do(ci => written = ci.ArgAt<string>(1));
            fileSystem.ReadAllText(KeyFilePath).Returns(_ => written);

            var (key, iv) = keySource.Load();

            fileSystem.Received(1).EnsureDirectoryExists("/some/where");
            fileSystem.Received(1).WriteAllTextOwnerOnly(KeyFilePath, Arg.Any<string>());
            fileSystem.DidNotReceive().WriteAllText(Arg.Any<string>(), Arg.Any<string>());
            fileSystem.DidNotReceive().RestrictFilePermissionsToOwner(Arg.Any<string>());

            key.Should().HaveCount(32, "AES-256");
            iv.Should().HaveCount(16);
            written.Should().Be(Convert.ToBase64String(key) + "." + Convert.ToBase64String(iv), "the format earlier versions wrote and still read");
        }

        [Test]
        public void GivenNoKeyFile_ThenEachMachineGetsADifferentKey()
        {
            fileSystem.FileExists(KeyFilePath).Returns(false);
            string written = null;
            fileSystem.When(x => x.WriteAllTextOwnerOnly(KeyFilePath, Arg.Any<string>())).Do(ci => written = ci.ArgAt<string>(1));
            fileSystem.ReadAllText(KeyFilePath).Returns(_ => written);

            var first = keySource.Load().Key;
            var second = new LinuxGeneratedMachineKey(log, fileSystem, KeyFilePath).Load().Key;

            first.Should().NotBeEquivalentTo(second);
        }

        [Test]
        public void GivenAnExistingKeyFile_ThenLoadsItWithoutWritingAnything()
        {
            fileSystem.FileExists(KeyFilePath).Returns(true);
            fileSystem.ReadAllText(KeyFilePath).Returns(ValidKeyFileContents);

            var (key, iv) = keySource.Load();

            Convert.ToBase64String(key).Should().Be("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");
            Convert.ToBase64String(iv).Should().Be("ZmVkY2JhOTg3NjU0MzIxMA==");
            fileSystem.DidNotReceive().WriteAllTextOwnerOnly(Arg.Any<string>(), Arg.Any<string>());
            fileSystem.DidNotReceive().WriteAllText(Arg.Any<string>(), Arg.Any<string>());
        }

        [Test]
        public void GivenAnExistingKeyFileOthersCanRead_ThenTightensItsPermissionsAndSaysSo()
        {
            fileSystem.FileExists(KeyFilePath).Returns(true);
            fileSystem.ReadAllText(KeyFilePath).Returns(ValidKeyFileContents);
            fileSystem.RestrictFilePermissionsToOwner(KeyFilePath).Returns(true);

            keySource.Load();

            fileSystem.Received(1).RestrictFilePermissionsToOwner(KeyFilePath);
            log.Received(1).Info(Arg.Is<string>(m => m.Contains(KeyFilePath) && m.Contains("only its owner")));
        }

        [Test]
        public void GivenAnExistingKeyFileAlreadyRestricted_ThenSaysNothing()
        {
            fileSystem.FileExists(KeyFilePath).Returns(true);
            fileSystem.ReadAllText(KeyFilePath).Returns(ValidKeyFileContents);
            fileSystem.RestrictFilePermissionsToOwner(KeyFilePath).Returns(false);

            keySource.Load();

            log.DidNotReceive().Info(Arg.Any<string>());
        }

        [Test]
        public void GivenThePermissionsCannotBeChanged_ThenTheKeyStillLoads()
        {
            fileSystem.FileExists(KeyFilePath).Returns(true);
            fileSystem.ReadAllText(KeyFilePath).Returns(ValidKeyFileContents);
            fileSystem.RestrictFilePermissionsToOwner(KeyFilePath).Throws(new UnauthorizedAccessException("read-only file system"));

            var (key, _) = keySource.Load();

            key.Should().NotBeEmpty();
            log.Received(1).Verbose(Arg.Any<UnauthorizedAccessException>(), Arg.Is<string>(m => m.Contains(KeyFilePath)));
            log.DidNotReceive().Warn(Arg.Any<string>());
            log.DidNotReceive().Warn(Arg.Any<Exception>(), Arg.Any<string>());
        }

        [Test]
        public void ThePermissionsAreOnlyCheckedOncePerInstance()
        {
            fileSystem.FileExists(KeyFilePath).Returns(true);
            fileSystem.ReadAllText(KeyFilePath).Returns(ValidKeyFileContents);

            keySource.Load();
            keySource.Load();
            keySource.Load();

            fileSystem.Received(1).RestrictFilePermissionsToOwner(KeyFilePath);
        }

        [TestCase("not base64 at all.nor this")]
        [TestCase("no-separator")]
        [TestCase("")]
        public void GivenACorruptKeyFile_ThenThrowsNamingTheFile(string contents)
        {
            fileSystem.FileExists(KeyFilePath).Returns(true);
            fileSystem.ReadAllText(KeyFilePath).Returns(contents);

            keySource.Invoking(x => x.Load())
                .Should().Throw<InvalidOperationException>()
                .WithMessage($"Machine key file at `{KeyFilePath}` is corrupt*");
        }

        [Test]
        public void GivenNoKeyFileAndToldNotToCreateOne_ThenThrowsWithoutWriting()
        {
            fileSystem.FileExists(KeyFilePath).Returns(false);
            var legacyLocation = new LinuxGeneratedMachineKey(log, fileSystem, KeyFilePath, createIfMissing: false);

            legacyLocation.Invoking(x => x.Load())
                .Should().Throw<InvalidOperationException>()
                .WithMessage($"There is no machine key file at `{KeyFilePath}`.");
            fileSystem.DidNotReceive().WriteAllTextOwnerOnly(Arg.Any<string>(), Arg.Any<string>());
            fileSystem.DidNotReceive().EnsureDirectoryExists(Arg.Any<string>());
        }

        [Test]
        public void DefaultsToEtcOctopus()
        {
            using (new TemporaryEnvironmentVariable(KubernetesConfig.NamespaceVariableName, null))
            using (new TemporaryEnvironmentVariable(EnvironmentVariables.TentacleMachineConfigurationHomeDirectory, null))
            {
                new LinuxGeneratedMachineKey(log, fileSystem).KeyFilePath.Should().Be("/etc/octopus/machinekey");
                LinuxGeneratedMachineKey.StandardKeyFilePath.Should().Be("/etc/octopus/machinekey");
            }
        }

        [Test]
        public void FollowsARelocatedMachineConfigurationHome()
        {
            // The same override a non-root install uses to move the instance registry out of /etc/octopus.
            using (new TemporaryEnvironmentVariable(KubernetesConfig.NamespaceVariableName, null))
            using (new TemporaryEnvironmentVariable(EnvironmentVariables.TentacleMachineConfigurationHomeDirectory, "/home/octopus/.octopus"))
            {
                new LinuxGeneratedMachineKey(log, fileSystem).KeyFilePath.Should().Be(Path.Combine("/home/octopus/.octopus", "machinekey"));
            }
        }

        [Test]
        public void UsesTentacleHomeWhenRunningAsTheKubernetesAgent()
        {
            using (new TemporaryEnvironmentVariable(KubernetesConfig.NamespaceVariableName, "octopus-agent"))
            using (new TemporaryEnvironmentVariable(EnvironmentVariables.TentacleHome, "/octopus"))
            using (new TemporaryEnvironmentVariable(EnvironmentVariables.TentacleMachineConfigurationHomeDirectory, "/somewhere/else"))
            {
                new LinuxGeneratedMachineKey(log, fileSystem).KeyFilePath.Should().Be(Path.Combine("/octopus", "machinekey"), "the Kubernetes agent's home is what is on the persistent volume");
            }
        }

        [Test]
        public void AnExplicitPathWinsOverTheEnvironment()
        {
            using (new TemporaryEnvironmentVariable(KubernetesConfig.NamespaceVariableName, "octopus-agent"))
            using (new TemporaryEnvironmentVariable(EnvironmentVariables.TentacleHome, "/octopus"))
            {
                keySource.KeyFilePath.Should().Be(KeyFilePath);
            }
        }

        class TemporaryEnvironmentVariable : IDisposable
        {
            readonly string name;
            readonly string previous;

            public TemporaryEnvironmentVariable(string name, string value)
            {
                this.name = name;
                previous = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, value);
            }

            public void Dispose()
            {
                Environment.SetEnvironmentVariable(name, previous);
            }
        }
    }
}
