using System;
using System.IO;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Util
{
    public class OctopusPhysicalFileSystemFixture
    {
        [Test]
        public void EnsureDiskHasEnoughFreeSpaceShouldWorkForStandardPath()
            => new OctopusPhysicalFileSystem(Substitute.For<ILog>()).EnsureDiskHasEnoughFreeSpace(Path.GetTempPath(), 0);

        [Test]
        public void EnsureDiskHasEnoughFreeSpaceShouldWorkButNotCheckForUncPaths()
            => new OctopusPhysicalFileSystem(Substitute.For<ILog>()).EnsureDiskHasEnoughFreeSpace(@"\\does\not\exist", long.MaxValue);

        [Test]
        public void EnsureDiskHasEnoughFreeSpaceThrowsAndExceptionIfThereIsNotEnoughSpace()
        {
            Action exec = () => new OctopusPhysicalFileSystem(Substitute.For<ILog>()).EnsureDiskHasEnoughFreeSpace(Path.GetTempPath(), long.MaxValue);
            exec.Should().Throw<IOException>().WithMessage("*does not have enough free disk space*");
        }

        [Test]
        public void DiskHasEnoughFreeSpace_UncPath_ShouldReturnTrue()
        {
            var actual = new OctopusPhysicalFileSystem(Substitute.For<ILog>()).DiskHasEnoughFreeSpace(@"\\does\not\exist");
            Assert.AreEqual(true, actual);
        }

        [Test]
        public void DiskHasEnoughFreeSpace_MaxValue_ShouldReturnFalse()
        {
            var actual = new OctopusPhysicalFileSystem(Substitute.For<ILog>()).DiskHasEnoughFreeSpace(Path.GetTempPath(), long.MaxValue);
            Assert.AreEqual(false, actual);
        }
    }
}