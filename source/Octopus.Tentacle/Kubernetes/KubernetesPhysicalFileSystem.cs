using System;
using System.IO;
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes
{
    public class KubernetesPhysicalFileSystem : OctopusPhysicalFileSystem
    {
        readonly IKubernetesDirectoryInformationProvider directoryInformationProvider;
        readonly IHomeConfiguration homeConfiguration;
        ISystemLog Log { get; }


        public KubernetesPhysicalFileSystem(IKubernetesDirectoryInformationProvider directoryInformationProvider,
            ISystemLog log,
            IHomeConfiguration homeConfiguration) : base(log)
        {
            this.directoryInformationProvider = directoryInformationProvider;
            Log = log;
            this.homeConfiguration = homeConfiguration;
        }

        public override void EnsureDiskHasEnoughFreeSpace(string directoryPath, long requiredSpaceInBytes)
        {
            var homeDir = homeConfiguration.HomeDirectory ?? throw new InvalidOperationException("Home directory is not set");
            var freeBytes = GetPathFreeBytes(homeDir);
            Log.Verbose($"Directory to be checked is {homeDir}, script directory is {directoryPath}, required space is {requiredSpaceInBytes} bytes");

            // If we can't get the free bytes, we just skip the check
            if (freeBytes is null) return;

            var required = requiredSpaceInBytes < 0 ? 0 : (ulong)requiredSpaceInBytes;
            // Make sure there is 10% (and a bit extra) more than we need
            required += required / 10 + 1024 * 1024;
            Log.Verbose($"Checking free space on disk. Required: {required} bytes, available: {freeBytes} bytes");
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