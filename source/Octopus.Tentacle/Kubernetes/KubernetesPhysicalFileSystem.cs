using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Octopus.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes
{
    public class KubernetesPhysicalFileSystem : OctopusPhysicalFileSystem
    {
        readonly IKubernetesDirectoryInformationProvider directoryInformationProvider;
        ISystemLog Log { get; }

        public KubernetesPhysicalFileSystem(IKubernetesDirectoryInformationProvider directoryInformationProvider, ISystemLog log) : base(log)
        {
            this.directoryInformationProvider = directoryInformationProvider;
            Log = log;
        }

        public override void EnsureDiskHasEnoughFreeSpace(string directoryPath, long requiredSpaceInBytes)
        {
            var freeBytes = GetPathFreeBytes(directoryPath);

            // If we can't get the free bytes, we just skip the check
            if (freeBytes is null) return;

            var required = requiredSpaceInBytes < 0 ? 0 : (ulong)requiredSpaceInBytes;
            // Make sure there is 10% (and a bit extra) more than we need
            required += required / 10 + 1024 * 1024;
            if (freeBytes < required)
            {
                throw new IOException($"Not enough free space on disk. Required: {required} bytes, available: {freeBytes} bytes");
            }
        }
        
        ulong? GetPathFreeBytes(string directoryPath)
        {
            var bytesUsed = directoryInformationProvider.GetPathUsedBytes(directoryPath);
            var bytesTotal = directoryInformationProvider.GetPathTotalBytes();
            if (bytesUsed.HasValue && bytesTotal.HasValue)
                return bytesTotal - bytesUsed;
            return null;
        }
    }
}