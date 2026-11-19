#if !NETFRAMEWORK
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Core.Util;
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
            keyFilePath = Path.Combine(directory, ConfigurationEncryptionKey.KeyFileName);
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
            var keySource = new ConfigurationEncryptionKey(log, fileSystem, keyFilePath);

            // Act
            keySource.CreateIfMissing();
            var key = keySource.Load();

            // Assert
            File.Exists(keyFilePath).Should().BeTrue("the directory is created on demand, as it always was");
            File.GetUnixFileMode(keyFilePath).Should().Be(OwnerReadWrite);
            File.ReadAllText(keyFilePath).Should().Be(Convert.ToBase64String(key));
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
                    var keySource = new ConfigurationEncryptionKey(log, new OctopusPhysicalFileSystem(log), keyFilePath);
                    keySource.CreateIfMissing();
                    return keySource.Load();
                }))
                .ToArray();

            // Act
            start.Set();
            var keys = await Task.WhenAll(loads);

            // Assert
            var onDisk = Convert.FromBase64String(File.ReadAllText(keyFilePath));
            keys.Should().AllSatisfy(k => k.Should().Equal(onDisk));
            Directory.GetFiles(directory).Should().ContainSingle("no racer's temporary file is left behind");
        }

        [Test]
        public void WhenRootCreatesTheKeyUnderAHardenedUmaskThenANonRootOwnerCanStillReadIt()
        {
            if (geteuid() != 0)
            {
                Assert.Ignore("Creating a key for another user needs root.");
            }

            // Arrange
            var user = "octo-key-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            CreateUser(user).Should().Be(0);
            var instanceKeyFilePath = Path.Combine(directory, "keys", "Tentacle", ConfigurationEncryptionKey.KeyFileName);
            var keySource = new ConfigurationEncryptionKey(log, fileSystem, instanceKeyFilePath);
            var previousUmask = umask(Convert.ToUInt32("027", 8));
            try
            {
                // Act
                keySource.CreateIfMissing();
                var owner = uint.Parse(RunAndRead("id", $"-u {user}"));
                UnixFile.SetOwnerIfRunningAsRoot(owner, keySource.KeyDirectory!);
                UnixFile.SetOwnerIfRunningAsRoot(owner, instanceKeyFilePath);

                // Assert
                Run("su", $"-s /bin/sh -c \"cat {instanceKeyFilePath}\" {user}").Should().Be(0, "the owner must be able to traverse the shared directories to its key");
                File.GetUnixFileMode(keySource.KeyDirectory!).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            finally
            {
                umask(previousUmask);
                DeleteUser(user);
            }
        }

        // Alpine has BusyBox's adduser and deluser rather than useradd and userdel.
        static bool IsAlpine => File.Exists("/etc/alpine-release");

        static int CreateUser(string user)
            => IsAlpine ? Run("adduser", $"-D -H {user}") : Run("useradd", $"--no-create-home {user}");

        static int DeleteUser(string user)
            => IsAlpine ? Run("deluser", user) : Run("userdel", user);

        static int Run(string fileName, string arguments)
        {
            using var process = Process.Start(fileName, arguments);
            process.WaitForExit();
            return process.ExitCode;
        }

        static string RunAndRead(string fileName, string arguments)
        {
            using var process = Process.Start(new ProcessStartInfo(fileName, arguments) { RedirectStandardOutput = true })!;
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return output;
        }

        [DllImport("libc")]
        static extern uint geteuid();

        [DllImport("libc")]
        static extern uint umask(uint mask);
    }
}
#endif
