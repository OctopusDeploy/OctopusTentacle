#if !NETFRAMEWORK
using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Tests.Support.TestAttributes;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Configuration.Crypto
{
    /// <summary>
    /// <see cref="LinuxGeneratedMachineKey"/> and the store that uses it against the real file system, in a temporary
    /// directory, to prove what they ask <see cref="OctopusPhysicalFileSystem"/> for actually lands on disk.
    /// </summary>
    [TestFixture]
    [LinuxTest]
    [SupportedOSPlatform("linux")]
    public class LinuxGeneratedMachineKeyFileFixture
    {
        const UnixFileMode OwnerReadWrite = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        const string ExistingKeyContents = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=.ZmVkY2JhOTg3NjU0MzIxMA==";

        string directory;
        string keyFilePath;
        ISystemLog log;
        OctopusPhysicalFileSystem fileSystem;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "octopus-machinekey-" + Guid.NewGuid());
            keyFilePath = Path.Combine(directory, "machinekey");
            log = Substitute.For<ISystemLog>();
            fileSystem = new OctopusPhysicalFileSystem(log);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }

        [Test]
        public void ANewKeyFileIsCreatedReadableOnlyByItsOwner()
        {
            var (key, iv) = new LinuxGeneratedMachineKey(log, fileSystem, keyFilePath).Load();

            File.Exists(keyFilePath).Should().BeTrue("the directory is created on demand, as it always was");
            File.GetUnixFileMode(keyFilePath).Should().Be(OwnerReadWrite);
            File.ReadAllText(keyFilePath).Should().Be(Convert.ToBase64String(key) + "." + Convert.ToBase64String(iv));
            Directory.GetFiles(directory).Should().ContainSingle("the temporary file the key is written to first is not left behind");
        }

        [Test]
        public void TheSameKeyIsLoadedNextTime()
        {
            var first = new LinuxGeneratedMachineKey(log, fileSystem, keyFilePath).Load();
            var second = new LinuxGeneratedMachineKey(log, fileSystem, keyFilePath).Load();

            second.Key.Should().Equal(first.Key);
            second.IV.Should().Equal(first.IV);
        }

        [Test]
        public async Task ManyProcessesCreatingTheKeyAtOnceAllEndUpWithTheSameKey()
        {
            // Stands in for several instances starting together after an upgrade: every one sees no key file and generates
            // its own. Only one may reach the file, and every one must then use that one.
            const int racers = 32;
            using var start = new ManualResetEventSlim(false);
            var loads = Enumerable.Range(0, racers)
                .Select(_ => Task.Run(() =>
                {
                    start.Wait();
                    return new LinuxGeneratedMachineKey(log, new OctopusPhysicalFileSystem(log), keyFilePath).Load().Key;
                }))
                .ToArray();
            start.Set();

            var keys = await Task.WhenAll(loads);

            var onDisk = Convert.FromBase64String(File.ReadAllText(keyFilePath).Split('.')[0]);
            keys.Should().AllSatisfy(k => k.Should().Equal(onDisk));
            Directory.GetFiles(directory).Should().ContainSingle("no racer's temporary file is left behind");
        }

        [Test]
        public void LoadingNeverChangesAnExistingKeyFilesPermissions()
        {
            // Loading happens in every command, including ones root runs for a service that runs as someone else.
            Directory.CreateDirectory(directory);
            File.WriteAllText(keyFilePath, ExistingKeyContents);
            var worldReadable = OwnerReadWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
            File.SetUnixFileMode(keyFilePath, worldReadable);

            var (key, _) = new LinuxGeneratedMachineKey(log, fileSystem, keyFilePath).Load();

            Convert.ToBase64String(key).Should().Be("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");
            File.GetUnixFileMode(keyFilePath).Should().Be(worldReadable);
        }

        [Test]
        public void AKeyFileLeftWorldReadableByAnEarlierVersionIsTightenedOnRequestWithoutChangingTheKey()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(keyFilePath, ExistingKeyContents);
            File.SetUnixFileMode(keyFilePath, OwnerReadWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

            new LinuxGeneratedMachineKey(log, fileSystem, keyFilePath).RestrictPermissionsToOwner();

            File.GetUnixFileMode(keyFilePath).Should().Be(OwnerReadWrite);
            File.ReadAllText(keyFilePath).Should().Be(ExistingKeyContents);
            log.Received(1).Info(Arg.Is<string>(m => m.Contains(keyFilePath)));
        }

        [Test]
        public void AKeyFileAlreadyRestrictedIsLeftAlone()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(keyFilePath, ExistingKeyContents);
            File.SetUnixFileMode(keyFilePath, OwnerReadWrite);
            var before = File.GetLastWriteTimeUtc(keyFilePath);

            new LinuxGeneratedMachineKey(log, fileSystem, keyFilePath).RestrictPermissionsToOwner();

            File.GetUnixFileMode(keyFilePath).Should().Be(OwnerReadWrite);
            File.GetLastWriteTimeUtc(keyFilePath).Should().Be(before);
            log.DidNotReceive().Info(Arg.Any<string>());
        }

        [Test]
        public void AConfigurationFileStoreKeepsItsKeyBesideTheFile()
        {
            // The composition production uses: no encryptor passed, so the store picks the one for its own file.
            Directory.CreateDirectory(directory);
            var configurationFile = Path.Combine(directory, "tentacle.config");
            File.WriteAllText(configurationFile, "<?xml version='1.0' encoding='UTF-8' ?><octopus-settings></octopus-settings>");

            new XmlFileKeyValueStore(fileSystem, configurationFile).Set<string>("Tentacle.Certificate", "the certificate", ProtectionLevel.MachineKey);

            File.GetUnixFileMode(keyFilePath).Should().Be(OwnerReadWrite, "the key is created beside the configuration it protects");
            var stored = XDocument.Load(configurationFile).Root!.Elements("set").Single(e => (string)e.Attribute("key") == "Tentacle.Certificate").Value;
            stored.Should().StartWith(LinuxMachineKeyEncryptor.ProtectedValuePrefix);
            new XmlFileKeyValueStore(fileSystem, configurationFile).Get<string>("Tentacle.Certificate", protectionLevel: ProtectionLevel.MachineKey)
                .Should().Be("the certificate", "a second process finds the same key beside the same file");
        }

        [Test]
        public void TryCreateFileOwnerOnlyNeverReplacesAnExistingFile()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(keyFilePath, "already here");

            fileSystem.TryCreateFileOwnerOnly(keyFilePath, "something else").Should().BeFalse();

            File.ReadAllText(keyFilePath).Should().Be("already here");
            Directory.GetFiles(directory).Should().ContainSingle();
        }
    }
}
#endif
