using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Core.Util;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes
{
    public class KubernetesPhysicalFileSystem : OctopusPhysicalFileSystem
    {
        readonly IKubernetesDirectoryInformationProvider directoryInformationProvider;
        ISystemLog Log { get; }

        // Set like this for now because we don't have a way to get the home directory from the provider without requiring ourselves
        // DI can be painful when circular dependencies happen with constructed classes :sad-panda:
        // When we can get an Injectable KubernetesConfiguration, we can remove this, alternatively, we can pull apart the configuration stores into different implementations
        readonly string HomeDir = Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleHome) ?? "/octopus";

        public KubernetesPhysicalFileSystem(IKubernetesDirectoryInformationProvider directoryInformationProvider,
            ISystemLog log) : base(log)
        {
            this.directoryInformationProvider = directoryInformationProvider;
            Log = log;
        }

        public override void EnsureDiskHasEnoughFreeSpace(string directoryPath, long requiredSpaceInBytes)
        {
            // Sync bridge: all hot-path callers (ScriptServiceV2, FileTransferService) now use
            // EnsureDiskHasEnoughFreeSpaceAsync. This sync overload remains for backward compat
            // and is only reached from inherently-sync boundaries (config stores) that never
            // exercise the Kubernetes path in production. The GetAwaiter here is acceptable
            // because those callers run on plain thread-pool workers with no SynchronizationContext.
            EnsureDiskHasEnoughFreeSpaceAsync(directoryPath, requiredSpaceInBytes).GetAwaiter().GetResult();
        }

        public override async Task EnsureDiskHasEnoughFreeSpaceAsync(string directoryPath, long requiredSpaceInBytes, CancellationToken cancellationToken = default)
        {
            var spaceInformation = await GetStorageInformationAsync();
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

        public async Task<(ulong freeSpaceBytes, ulong totalSpaceBytes)?> GetStorageInformationAsync()
        {
            var bytesUsed = await directoryInformationProvider.GetPathUsedBytesAsync(HomeDir);
            var bytesTotal = directoryInformationProvider.GetPathTotalBytes();
            if (bytesUsed.HasValue && bytesTotal.HasValue)
            {
                return (bytesTotal.Value - bytesUsed.Value, bytesTotal.Value);
            }

            return null;
        }
    }
}