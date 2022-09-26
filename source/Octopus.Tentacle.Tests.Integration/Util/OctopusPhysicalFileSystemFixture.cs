using System;
using System.IO;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Util
{
    public class OctopusPhysicalFileSystemFixture
    {
        [Test]
        public void EnsureDiskHasEnoughFreeSpaceShouldWorkForStandardPath()
        {
            new OctopusPhysicalFileSystem(Substitute.For<ISystemLog>()).EnsureDiskHasEnoughFreeSpace(Path.GetTempPath(), 0);
        }

        [Test]
        public void EnsureDiskHasEnoughFreeSpaceShouldWorkButNotCheckForUncPaths()
        {
            new OctopusPhysicalFileSystem(Substitute.For<ISystemLog>()).EnsureDiskHasEnoughFreeSpace(@"\\does\not\exist", long.MaxValue);
        }

        [Test]
        public void EnsureDiskHasEnoughFreeSpaceThrowsAndExceptionIfThereIsNotEnoughSpace()
        {
            Action exec = () => new OctopusPhysicalFileSystem(Substitute.For<ISystemLog>()).EnsureDiskHasEnoughFreeSpace(Path.GetTempPath(), long.MaxValue);
            exec.Should().Throw<IOException>().WithMessage("*does not have enough free disk space*");
        }

        [Test]
        public void DeleteDirectory_WithReadOnlyFiles_ShouldSucceed()
        {
            // Arrange
            var readonlyDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var readonlyFile = Path.Combine(readonlyDir, "test-readonly.txt");

            Directory.CreateDirectory(readonlyDir);
            File.AppendAllText(
                readonlyFile,
                "Contents of a readonly file"
            );

            File.SetAttributes(readonlyDir, FileAttributes.ReadOnly);
            File.SetAttributes(readonlyFile, FileAttributes.ReadOnly);

            try
            {
                // Act
                var actual = new OctopusPhysicalFileSystem(Substitute.For<ISystemLog>());
                actual.DeleteDirectory(readonlyDir);

                // Assert
                new DirectoryInfo(readonlyDir).Exists.Should().BeFalse();
            }
            catch
            {
                // Clean up temp folder if test fails
                if (File.Exists(readonlyFile))
                    File.SetAttributes(readonlyFile, FileAttributes.Normal);

                if (Directory.Exists(readonlyDir))
                {
                    File.SetAttributes(readonlyDir, FileAttributes.Normal);
                    Directory.Delete(readonlyDir, true);
                }

                throw;
            }
        }
    }
}