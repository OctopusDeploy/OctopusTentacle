#if !NETFRAMEWORK
using System;
using System.IO;
using System.Runtime.Versioning;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Tests.Support;
using Octopus.Tentacle.Tests.Support.TestAttributes;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Configuration.Crypto
{
    [LinuxTest]
    [SupportedOSPlatform("linux")]
    public class LinuxConfigurationEncryptionKeyFileTests
    {
        const UnixFileMode OwnerReadWrite = UnixFileMode.UserRead | UnixFileMode.UserWrite;

        [Test]
        public void WhenTheKeyIsCreatedThenOnlyItsOwnerCanReadIt()
        {
            // Arrange
            var log = Substitute.For<ISystemLog>();
            var fileSystem = new OctopusPhysicalFileSystem(log);
            using var directory = new TemporaryDirectory(fileSystem);
            var keyDirectory = Path.Combine(directory.DirectoryPath, "keys");
            var keyFilePath = Path.Combine(keyDirectory, ConfigurationEncryptionKey.KeyFileName);
            var keySource = new ConfigurationEncryptionKey(log, fileSystem, keyFilePath);

            // Act
            keySource.CreateIfMissing();
            var key = keySource.Load();

            // Assert
            File.Exists(keyFilePath).Should().BeTrue("the directory is created on demand, as it always was");
            File.GetUnixFileMode(keyFilePath).Should().Be(OwnerReadWrite);
            File.ReadAllText(keyFilePath).Should().Be(Convert.ToBase64String(key));
            Directory.GetFiles(keyDirectory).Should().ContainSingle("the temporary file the key is written to first is not left behind");
        }
    }
}
#endif
