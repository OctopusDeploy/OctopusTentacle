using System;
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
        const string ConfigurationFile = "/some/where/tentacle.config";
        const string KeyFilePath = "/some/where/config-encryption.key";
        const string ValidKeyFileContents = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=.ZmVkY2JhOTg3NjU0MzIxMA==";

        IOctopusFileSystem fileSystem;
        ISystemLog log;
        LinuxGeneratedMachineKey keySource;
        string written;

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<IOctopusFileSystem>();
            log = Substitute.For<ISystemLog>();
            keySource = new LinuxGeneratedMachineKey(log, fileSystem, KeyFilePath, ownerReferencePath: ConfigurationFile);

            // A file system where the key file does not exist until something creates it.
            written = null;
            fileSystem.FileExists(KeyFilePath).Returns(_ => written != null);
            fileSystem.TryCreateFileOwnerOnly(KeyFilePath, Arg.Any<string>()).Returns(ci =>
            {
                if (written != null)
                    return false;
                written = ci.ArgAt<string>(1);
                return true;
            });
            fileSystem.ReadAllText(KeyFilePath).Returns(_ => written);
        }

        [Test]
        public void TheKeyForAConfigurationFileIsBesideIt()
        {
            LinuxGeneratedMachineKey.KeyFilePathFor("/etc/octopus/Tentacle/tentacle-Tentacle.config").Should().Be("/etc/octopus/Tentacle/config-encryption.key");
            LinuxGeneratedMachineKey.KeyFilePathFor("/etc/octopus/tentacle.config").Should().Be("/etc/octopus/config-encryption.key", "the official Docker image's configuration: beside the legacy /etc/octopus/machinekey, never the same file");
            LinuxGeneratedMachineKey.KeyFilePathFor("/tentacle.config").Should().Be("/config-encryption.key");
        }

        [Test]
        public void GivenNoKeyFile_ThenGeneratesOneAtomicallyThatOnlyTheOwnerCanRead()
        {
            var (key, iv) = keySource.Load();

            fileSystem.Received(1).EnsureDirectoryExists("/some/where");
            fileSystem.Received(1).TryCreateFileOwnerOnly(KeyFilePath, Arg.Any<string>());
            fileSystem.DidNotReceive().WriteAllText(Arg.Any<string>(), Arg.Any<string>());
            fileSystem.DidNotReceive().WriteAllTextOwnerOnly(Arg.Any<string>(), Arg.Any<string>());
            fileSystem.DidNotReceive().RestrictFilePermissionsToOwner(Arg.Any<string>());

            key.Should().HaveCount(32, "AES-256");
            iv.Should().HaveCount(16);
            written.Should().Be(Convert.ToBase64String(key) + "." + Convert.ToBase64String(iv), "the format earlier versions wrote and still read");
            log.Received(1).Info(Arg.Is<string>(m => m.Contains(KeyFilePath) && m.Contains("Keep it with the configuration file")));
        }

        [Test]
        public void GivenNoKeyFile_ThenEachMachineGetsADifferentKey()
        {
            var first = keySource.Load().Key;
            written = null;
            var second = new LinuxGeneratedMachineKey(log, fileSystem, KeyFilePath).Load().Key;

            first.Should().NotBeEquivalentTo(second);
        }

        [Test]
        public void GivenAnotherProcessCreatesTheKeyFirst_ThenItsKeyIsUsed()
        {
            // Nothing there when we look, but another process moves its key into place before we can.
            var exists = false;
            fileSystem.FileExists(KeyFilePath).Returns(_ => exists);
            fileSystem.TryCreateFileOwnerOnly(KeyFilePath, Arg.Any<string>()).Returns(_ =>
            {
                exists = true;
                return false;
            });
            fileSystem.ReadAllText(KeyFilePath).Returns(ValidKeyFileContents);

            var (key, _) = keySource.Load();

            Convert.ToBase64String(key).Should().Be("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=", "whichever key reached the file is the one every process must use");
            log.DidNotReceive().Info(Arg.Any<string>());
            fileSystem.DidNotReceive().TryChangeOwnerToMatch(Arg.Any<string>(), Arg.Any<string>());
        }

        [Test]
        public void GivenNoKeyFile_ThenTheNewKeyIsGivenTheConfigurationFilesOwner()
        {
            // So `sudo tentacle new-certificate` on a configuration that belongs to the service user leaves a key that user can read.
            fileSystem.FileExists(ConfigurationFile).Returns(true);

            keySource.Load();

            fileSystem.Received(1).TryChangeOwnerToMatch(KeyFilePath, ConfigurationFile);
        }

        [Test]
        public void GivenNoConfigurationFileYet_ThenOwnershipIsLeftAlone()
        {
            fileSystem.FileExists(ConfigurationFile).Returns(false);

            keySource.Load();

            fileSystem.DidNotReceive().TryChangeOwnerToMatch(Arg.Any<string>(), Arg.Any<string>());
        }

        [Test]
        public void GivenAnExistingKeyFile_ThenLoadsItWithoutWritingOrChangingAnything()
        {
            written = ValidKeyFileContents;

            var (key, iv) = keySource.Load();

            Convert.ToBase64String(key).Should().Be("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");
            Convert.ToBase64String(iv).Should().Be("ZmVkY2JhOTg3NjU0MzIxMA==");
            fileSystem.DidNotReceive().TryCreateFileOwnerOnly(Arg.Any<string>(), Arg.Any<string>());
            fileSystem.DidNotReceive().WriteAllText(Arg.Any<string>(), Arg.Any<string>());
            fileSystem.DidNotReceive().RestrictFilePermissionsToOwner(Arg.Any<string>());
            fileSystem.DidNotReceive().TryChangeOwnerToMatch(Arg.Any<string>(), Arg.Any<string>());
        }

        [Test]
        public void TheKeyIsReadOncePerInstance()
        {
            written = ValidKeyFileContents;

            keySource.Load();
            keySource.Load();
            keySource.Load();

            fileSystem.Received(1).ReadAllText(KeyFilePath);
        }

        [Test]
        public void GivenAKeyFileTheCurrentUserCannotRead_ThenTheErrorSaysHowToFixIt()
        {
            written = ValidKeyFileContents;
            fileSystem.ReadAllText(KeyFilePath).Throws(new UnauthorizedAccessException("Access to the path is denied."));

            keySource.Invoking(x => x.Load())
                .Should().Throw<InvalidOperationException>()
                .WithMessage($"The machine key file `{KeyFilePath}` exists but the user Tentacle is running as ({Environment.UserName}) cannot read it.*chown*service --install --username*")
                .WithInnerException<UnauthorizedAccessException>();
        }

        [Test]
        public void RestrictingPermissions_TightensAnExistingKeyFileAndSaysSo()
        {
            written = ValidKeyFileContents;
            fileSystem.RestrictFilePermissionsToOwner(KeyFilePath).Returns(true);

            keySource.RestrictPermissionsToOwner();

            fileSystem.Received(1).RestrictFilePermissionsToOwner(KeyFilePath);
            log.Received(1).Info(Arg.Is<string>(m => m.Contains(KeyFilePath) && m.Contains("only its owner")));
        }

        [Test]
        public void RestrictingPermissions_OnAnAlreadyRestrictedFileSaysNothing()
        {
            written = ValidKeyFileContents;
            fileSystem.RestrictFilePermissionsToOwner(KeyFilePath).Returns(false);

            keySource.RestrictPermissionsToOwner();

            log.DidNotReceive().Info(Arg.Any<string>());
        }

        [Test]
        public void RestrictingPermissions_WhenTheyCannotBeChanged_IsLoggedQuietlyAndTheKeyStillLoads()
        {
            written = ValidKeyFileContents;
            fileSystem.RestrictFilePermissionsToOwner(KeyFilePath).Throws(new UnauthorizedAccessException("read-only file system"));

            keySource.Invoking(x => x.RestrictPermissionsToOwner()).Should().NotThrow();

            keySource.Load().Key.Should().NotBeEmpty();
            log.Received(1).Verbose(Arg.Any<UnauthorizedAccessException>(), Arg.Is<string>(m => m.Contains(KeyFilePath)));
            log.DidNotReceive().Warn(Arg.Any<string>());
            log.DidNotReceive().Warn(Arg.Any<Exception>(), Arg.Any<string>());
        }

        [Test]
        public void RestrictingPermissions_WithNoKeyFile_DoesNothing()
        {
            keySource.RestrictPermissionsToOwner();

            fileSystem.DidNotReceive().RestrictFilePermissionsToOwner(Arg.Any<string>());
            written.Should().BeNull("restricting permissions must not create a key");
        }

        [TestCase("not base64 at all.nor this")]
        [TestCase("no-separator")]
        [TestCase("")]
        [TestCase(".ZmVkY2JhOTg3NjU0MzIxMA==")]
        public void GivenACorruptKeyFile_ThenThrowsNamingTheFile(string contents)
        {
            written = contents;

            keySource.Invoking(x => x.Load())
                .Should().Throw<InvalidOperationException>()
                .WithMessage($"Machine key file at `{KeyFilePath}` is corrupt*");
        }

        [Test]
        public void GivenNoKeyFileAndToldNotToCreateOne_ThenThrowsWithoutWriting()
        {
            var legacyLocation = new LinuxGeneratedMachineKey(log, fileSystem, KeyFilePath, createIfMissing: false);

            legacyLocation.Invoking(x => x.Load())
                .Should().Throw<InvalidOperationException>()
                .WithMessage($"There is no machine key file at `{KeyFilePath}`.");
            fileSystem.DidNotReceive().TryCreateFileOwnerOnly(Arg.Any<string>(), Arg.Any<string>());
            fileSystem.DidNotReceive().EnsureDirectoryExists(Arg.Any<string>());
        }

        [Test]
        public void TheLegacyKeyWasAtEtcOctopus()
        {
            using (new TemporaryEnvironmentVariable(KubernetesConfig.NamespaceVariableName, null))
            using (new TemporaryEnvironmentVariable(EnvironmentVariables.TentacleMachineConfigurationHomeDirectory, "/home/octopus/.octopus"))
            {
                LinuxGeneratedMachineKey.LegacyKeyFilePathForThisHost.Should().Be("/etc/octopus/machinekey", "earlier versions ignored the machine configuration home for the key");
                LinuxGeneratedMachineKey.LegacyKeyFilePath.Should().Be("/etc/octopus/machinekey");
            }
        }

        [Test]
        public void TheKubernetesAgentsLegacyKeyWasInTentacleHome()
        {
            using (new TemporaryEnvironmentVariable(KubernetesConfig.NamespaceVariableName, "octopus-agent"))
            using (new TemporaryEnvironmentVariable(EnvironmentVariables.TentacleHome, "/octopus/"))
            {
                LinuxGeneratedMachineKey.LegacyKeyFilePathForThisHost.Should().Be("/octopus/machinekey");
            }
        }

        [Test]
        public void GivingTheKeyToTheServiceUser_CreatesItIfNeededAndChangesItsOwner()
        {
            fileSystem.TryChangeOwner(KeyFilePath, "tentacle").Returns(true);

            LinuxGeneratedMachineKey.GiveKeyAndConfigurationToServiceUser(log, fileSystem, ConfigurationFile, "tentacle").Should().BeTrue();

            written.Should().NotBeNull("a service user that cannot create the key must be given one");
            fileSystem.Received(1).TryChangeOwner(KeyFilePath, "tentacle");
            log.Received(1).Info(Arg.Is<string>(m => m.Contains(KeyFilePath) && m.Contains("tentacle")));
        }

        [Test]
        public void GivingTheKeyToTheServiceUser_WhenTheOwnerCannotBeChanged_WarnsWithTheFix()
        {
            written = ValidKeyFileContents;
            fileSystem.TryChangeOwner(KeyFilePath, "tentacle").Returns(false);

            LinuxGeneratedMachineKey.GiveKeyAndConfigurationToServiceUser(log, fileSystem, ConfigurationFile, "tentacle").Should().BeFalse();

            log.Received(1).Warn(Arg.Is<string>(m => m.Contains($"sudo chown tentacle {KeyFilePath}")));
        }

        [Test]
        public void GivingTheKeyToTheServiceUser_AlsoGivesItTheConfiguration()
        {
            // The agent restricts the configuration to its owner, so a new service user must own it to read it.
            fileSystem.FileExists(ConfigurationFile).Returns(true);
            fileSystem.TryChangeOwner(KeyFilePath, "tentacle").Returns(true);
            fileSystem.TryChangeOwner(ConfigurationFile, "tentacle").Returns(true);

            LinuxGeneratedMachineKey.GiveKeyAndConfigurationToServiceUser(log, fileSystem, ConfigurationFile, "tentacle").Should().BeTrue();

            fileSystem.Received(1).TryChangeOwner(ConfigurationFile, "tentacle");
            log.Received(1).Info(Arg.Is<string>(m => m.Contains(ConfigurationFile) && m.Contains("tentacle")));
        }

        [Test]
        public void GivingTheKeyToTheServiceUser_WhenTheConfigurationCannotBeGivenAway_WarnsWithTheFix()
        {
            fileSystem.FileExists(ConfigurationFile).Returns(true);
            fileSystem.TryChangeOwner(KeyFilePath, "tentacle").Returns(true);
            fileSystem.TryChangeOwner(ConfigurationFile, "tentacle").Returns(false);

            LinuxGeneratedMachineKey.GiveKeyAndConfigurationToServiceUser(log, fileSystem, ConfigurationFile, "tentacle").Should().BeFalse();

            log.Received(1).Warn(Arg.Is<string>(m => m.Contains($"sudo chown tentacle {ConfigurationFile}")));
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
