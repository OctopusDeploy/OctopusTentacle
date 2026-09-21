#if !NETFRAMEWORK
using System;
using System.IO;
using System.Runtime.Versioning;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Tests.Support.TestAttributes;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Configuration.Crypto
{
    /// <summary>
    /// <see cref="LinuxGeneratedMachineKey"/> against the real file system, in a temporary directory, to prove the
    /// permissions it asks <see cref="OctopusPhysicalFileSystem"/> for actually land on disk.
    /// </summary>
    [TestFixture]
    [LinuxTest]
    [SupportedOSPlatform("linux")]
    public class LinuxGeneratedMachineKeyFileFixture
    {
        const UnixFileMode OwnerReadWrite = UnixFileMode.UserRead | UnixFileMode.UserWrite;

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
        public void AKeyFileLeftWorldReadableByAnEarlierVersionIsTightenedWithoutChangingTheKey()
        {
            Directory.CreateDirectory(directory);
            const string contents = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=.ZmVkY2JhOTg3NjU0MzIxMA==";
            File.WriteAllText(keyFilePath, contents);
            File.SetUnixFileMode(keyFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

            var (key, _) = new LinuxGeneratedMachineKey(log, fileSystem, keyFilePath).Load();

            File.GetUnixFileMode(keyFilePath).Should().Be(OwnerReadWrite);
            File.ReadAllText(keyFilePath).Should().Be(contents);
            Convert.ToBase64String(key).Should().Be("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");
            log.Received(1).Info(Arg.Is<string>(m => m.Contains(keyFilePath)));
        }

        [Test]
        public void AKeyFileAlreadyRestrictedIsLeftAlone()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(keyFilePath, "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=.ZmVkY2JhOTg3NjU0MzIxMA==");
            File.SetUnixFileMode(keyFilePath, OwnerReadWrite);
            var before = File.GetLastWriteTimeUtc(keyFilePath);

            new LinuxGeneratedMachineKey(log, fileSystem, keyFilePath).Load();

            File.GetUnixFileMode(keyFilePath).Should().Be(OwnerReadWrite);
            File.GetLastWriteTimeUtc(keyFilePath).Should().Be(before);
            log.DidNotReceive().Info(Arg.Any<string>());
        }
    }
}
#endif
