#if !NETFRAMEWORK
using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Tests.Support;
using Octopus.Tentacle.Tests.Support.TestAttributes;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Configuration.Crypto
{
    [TestFixture]
    [LinuxTest]
    [SupportedOSPlatform("linux")]
    public class LinuxConfigurationFileSecurityFixture
    {
        const UnixFileMode OwnerReadWrite = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        const UnixFileMode WorldReadable = OwnerReadWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

        string directory;
        string keyFilePath;
        ISystemLog log;
        OctopusPhysicalFileSystem fileSystem;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "octopus-config-encryption-key-" + Guid.NewGuid());
            keyFilePath = Path.Combine(directory, LinuxGeneratedMachineKey.KeyFileName);
            log = Substitute.For<ISystemLog>();
            fileSystem = new OctopusPhysicalFileSystem(log);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Test]
        public void WhenTheKeyIsCreatedThenOnlyItsOwnerCanReadIt()
        {
            // Arrange
            var keySource = new LinuxGeneratedMachineKey(log, fileSystem, keyFilePath);

            // Act
            var (key, iv) = keySource.Load();

            // Assert
            File.Exists(keyFilePath).Should().BeTrue("the directory is created on demand, as it always was");
            File.GetUnixFileMode(keyFilePath).Should().Be(OwnerReadWrite);
            File.ReadAllText(keyFilePath).Should().Be(Convert.ToBase64String(key) + "." + Convert.ToBase64String(iv));
            Directory.GetFiles(directory).Should().ContainSingle("the temporary file the key is written to first is not left behind");
        }

        [Test]
        public async Task WhenManyProcessesCreateTheKeyAtOnceThenTheyAllGetTheSameKey()
        {
            // Arrange
            const int racers = 32;
            using var start = new ManualResetEventSlim(false);
            var loads = Enumerable.Range(0, racers)
                .Select(_ => Task.Run(() =>
                {
                    start.Wait();
                    return new LinuxGeneratedMachineKey(log, new OctopusPhysicalFileSystem(log), keyFilePath).Load().Key;
                }))
                .ToArray();

            // Act
            start.Set();
            var keys = await Task.WhenAll(loads);

            // Assert
            var onDisk = Convert.FromBase64String(File.ReadAllText(keyFilePath).Split('.')[0]);
            keys.Should().AllSatisfy(k => k.Should().Equal(onDisk));
            Directory.GetFiles(directory).Should().ContainSingle("no racer's temporary file is left behind");
        }

        [Test]
        public void WhenRunningAsRootThenASymlinkTargetIsNeverWrittenRestrictedOrChowned()
        {
            // Arrange
            IgnoreUnlessRoot();
            const string emptyConfiguration = "<?xml version='1.0' encoding='UTF-8' ?><octopus-settings></octopus-settings>";
            var target = CreateWorldReadableFile("not-tentacles", emptyConfiguration);
            var configurationFile = Path.Combine(directory, "tentacle.config");
            File.CreateSymbolicLink(configurationFile, target);
            var store = new XmlFileKeyValueStore(fileSystem, configurationFile, encryptor: TestMachineKeyEncryptor.Current);

            // Act
            var restrictError = CaptureException(() => fileSystem.RestrictFilePermissionsToOwner(configurationFile));
            var ownerChanged = fileSystem.TryChangeOwner(configurationFile, "nobody", out _);
            store.Set<string>("a", "b");

            // Assert
            restrictError.Should().BeOfType<IOException>();
            ownerChanged.Should().BeFalse();
            File.ReadAllText(target).Should().Be(emptyConfiguration);
            File.GetUnixFileMode(target).Should().Be(WorldReadable);
            new FileInfo(configurationFile).LinkTarget.Should().BeNull();
            File.GetUnixFileMode(configurationFile).Should().Be(OwnerReadWrite);
        }

        static void IgnoreUnlessRoot()
        {
            if (!Environment.IsPrivilegedProcess)
            {
                Assert.Ignore("Only root has to guard against a path another user swapped for a symlink.");
            }
        }

        static Exception? CaptureException(Action action)
        {
            try
            {
                action();
                return null;
            }
            catch (Exception e)
            {
                return e;
            }
        }

        string CreateWorldReadableFile(string name, string contents)
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, name);
            File.WriteAllText(path, contents);
            File.SetUnixFileMode(path, WorldReadable);
            return path;
        }
    }
}
#endif
