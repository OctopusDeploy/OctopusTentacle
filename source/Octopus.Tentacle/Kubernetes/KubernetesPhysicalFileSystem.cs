using System;
using System.IO;
using Octopus.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes
{
    public class KubernetesPhysicalFileSystem : OctopusPhysicalFileSystem
    {
        readonly IKubernetesDirectoryInformationProvider directoryInformationProvider;
        ISystemLog Log { get; }

        (ulong freeSpaceBytes, ulong totalSpaceBytes)? spaceInformationCache;
        
        // Set like this for now because we don't have a way to get the home directory from the provider without requiring ourselves
        // DI can be painful when circular dependencies happen with constructed classes :sad-panda:
        // When we can get an Injectable KubernetesConfiguration, we can remove this, alternatively, we can pull apart the configuration stores into different implementations
        const string HomeDir = "/octopus";

        public KubernetesPhysicalFileSystem(IKubernetesDirectoryInformationProvider directoryInformationProvider,
            ISystemLog log) : base(log)
        {
            this.directoryInformationProvider = directoryInformationProvider;
            Log = log;
        }

        public override void EnsureDiskHasEnoughFreeSpace(string directoryPath, long requiredSpaceInBytes)
        {
            var spaceInformation = GetSpaceInformation(useCache: false);
            Log.Verbose($"Directory to be checked is {HomeDir}, script directory is {directoryPath}, required space is {requiredSpaceInBytes} bytes");

            // If we can't get the free bytes, we just skip the check
            if (spaceInformation is null) return;

            var freeBytes = spaceInformation.Value.freeSpaceBytes;

            var required = requiredSpaceInBytes < 0 ? 0 : (ulong)requiredSpaceInBytes;
            // Make sure there is 10% (and a bit extra) more than we need
            required += required / 10 + 1024 * 1024;
            Log.Verbose($"Checking free space on disk. Required: {required} bytes, available: {freeBytes} bytes");
            if (freeBytes < required)
            {
                throw new IOException($"Not enough free space on disk. Required: {required} bytes, available: {freeBytes} bytes");
            }
        }

        public (ulong freeSpaceBytes, ulong totalSpaceBytes)? GetCachedSpaceInformation()
        {
            return GetSpaceInformation(useCache: true);
        }

        (ulong freeSpaceBytes, ulong totalSpaceBytes)? GetSpaceInformation(bool useCache)
        {
            if (useCache && spaceInformationCache is not null)
            {
                return spaceInformationCache;
            }

            var bytesUsed = directoryInformationProvider.GetPathUsedBytes(HomeDir);
            var bytesTotal = directoryInformationProvider.GetPathTotalBytes();
            if (bytesUsed.HasValue && bytesTotal.HasValue)
                return spaceInformationCache = (bytesTotal.Value - bytesUsed.Value, bytesTotal.Value);
            return spaceInformationCache = null;
        }
    }
}