using System.Collections.Generic;
using System.IO;
using System.Linq;
using Octopus.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes
{
    public class KubernetesPhysicalFileSystem : OctopusPhysicalFileSystem
    {
        ISystemLog Log { get; }

        public KubernetesPhysicalFileSystem(ISystemLog log) : base(log)
        {
            Log = log;
        }

        public override void EnsureDiskHasEnoughFreeSpace(string directoryPath, long requiredSpaceInBytes)
        {
            if (!PlatformDetection.Kubernetes.IsRunningAsKubernetesAgent)
            {
                Log.Error("Not running as Kubernetes agent, using default implementation");
                base.EnsureDiskHasEnoughFreeSpace(directoryPath, requiredSpaceInBytes);
            }
            
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

        ulong? GetDriveBytesUsingDu(string directoryPath)
        {
            var stdOut = new List<string>();
            var stdErr = new List<string>();
            var exitCode = SilentProcessRunner.ExecuteCommand("du", $"-s -B 1 {directoryPath}", "/", stdOut.Add, stdErr.Add);


            if (exitCode != 0)
            {
                Log.Warn("Could not reliably get disk space using du. Getting best approximation...");
                Log.Info($"Du stdout returned {stdOut}");
                Log.Info($"Du stderr returned {stdErr}");
            }

            var lineWithDirectory = stdOut.SingleOrDefault(outputLine => outputLine.Contains(directoryPath));
                
            if (ulong.TryParse(lineWithDirectory?.Split('\t')[0], out var bytes))
                return bytes;
            return null;
        }
        
        ulong? GetTotalBytesOfClaim(string volumeSize)
        {
            var persistentVolumeSize = new k8s.Models.ResourceQuantity(volumeSize);
            return persistentVolumeSize.ToUInt64();
        }

        ulong? GetPathFreeBytes(string directoryPath)
        {
            var bytesUsed = GetDriveBytesUsingDu(directoryPath);
            var bytesTotal = GetTotalBytesOfClaim(KubernetesConfig.PersistentVolumeSize);
            if (bytesUsed.HasValue && bytesTotal.HasValue)
                return bytesTotal - bytesUsed;
            return null;
        }
    }
}