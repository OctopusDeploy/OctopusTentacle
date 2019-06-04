using System;
using System.IO;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Util
{
    public class OctopusPhysicalFileSystemFixture
    {
        [Test]
        public void EnsureDiskHasEnoughFreeSpaceShouldWorkForStandardPath()
         => new OctopusPhysicalFileSystem().EnsureDiskHasEnoughFreeSpace(Path.GetTempPath(), 0);
        
        [Test]
        public void EnsureDiskHasEnoughFreeSpaceShouldWorkButNotCheckForUncPaths()
            => new OctopusPhysicalFileSystem().EnsureDiskHasEnoughFreeSpace(@"\\does\not\exist", long.MaxValue);

        
        [Test]
        public void EnsureDiskHasEnoughFreeSpaceThrowsAndExceptionIfThereIsNotEnoughSpace()
        {
            Action exec = () => new OctopusPhysicalFileSystem().EnsureDiskHasEnoughFreeSpace(Path.GetTempPath(), long.MaxValue);
            exec.Should().Throw<IOException>().WithMessage("*does not have enough free disk space*");
        }
    }
}