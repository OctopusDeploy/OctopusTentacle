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
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Core.Diagnostics;
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
            keySource.CreateIfMissing();
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
                    var keySource = new LinuxGeneratedMachineKey(log, new OctopusPhysicalFileSystem(log), keyFilePath);
                    keySource.CreateIfMissing();
                    return keySource.Load().Key;
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
    }
}
#endif
