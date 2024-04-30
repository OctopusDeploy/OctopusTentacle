using System;
using System.IO;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Tests.Kubernetes
{
    // [TestFixture]
    // public class KubernetesPhysicalFileSystemFixture
    // {
    //     const ulong Megabyte = 1000 * 1000;
    //     const ulong Mebibyte = 1024 * 1024;
    //     const ulong Gibibyte = 1024 * 1024 * 1024;
    //
    //     [TestCase(100 * Mebibyte, 300 * Mebibyte, true)]
    //     [TestCase(100 * Mebibyte, 800 * Mebibyte, false)]
    //     [TestCase(800 * Megabyte, 1 * Gibibyte, true)]
    //     public void DiskSpaceUsed(ulong diskSpaceUsed, ulong totalDiskSpace, bool throwException)
    //     {
    //         const string directoryPath = "/octopus";
    //         
    //         var directoryInformationProvider = Substitute.For<IKubernetesDirectoryInformationProvider>();
    //         directoryInformationProvider.GetPathTotalBytes().Returns(totalDiskSpace);
    //         directoryInformationProvider.GetPathUsedBytes("/octopus").Returns(diskSpaceUsed);
    //         
    //         var homeConfiguration = Substitute.For<IHomeConfiguration>();
    //         homeConfiguration.HomeDirectory.Returns("/octopus");
    //         
    //         var fileSystem = new KubernetesPhysicalFileSystem(directoryInformationProvider, Substitute.For<ISystemLog>());
    //         
    //         if (throwException)
    //         {
    //             Action a = () => fileSystem.EnsureDiskHasEnoughFreeSpace(directoryPath);
    //             a.Should().Throw<IOException>();
    //         }
    //         else
    //         {
    //             Action a = () => fileSystem.EnsureDiskHasEnoughFreeSpace(directoryPath);
    //             a.Should().NotThrow();
    //         }
    //     }
    // }
}