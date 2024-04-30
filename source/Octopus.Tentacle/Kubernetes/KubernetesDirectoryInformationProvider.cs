using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesDirectoryInformationProvider
    {
        public ulong? GetPathUsedBytes(string directoryPath);
        public ulong? GetPathTotalBytes();
    }

    public class KubernetesDirectoryInformationCache
    {
        ulong? UsedBytes { get; set; }
        DateTime LastUpdated { get; set; }
        
        public void SetCache(ulong? usedBytes)
        {
            UsedBytes = usedBytes;
            LastUpdated = DateTime.Now;
        }
        
        public bool TryGetCache(out ulong? usedBytes)
        {
            if (DateTime.Now - LastUpdated > TimeSpan.FromMinutes(1))
            {
                usedBytes = UsedBytes;
                return false;
            }
            usedBytes = null;
            return true;
        }
    }

    public class KubernetesDirectoryInformationProvider : IKubernetesDirectoryInformationProvider
    {
        readonly ISystemLog log;
        readonly ISilentProcessRunner silentProcessRunner;
        readonly KubernetesDirectoryInformationCache directoryInformationCache = new();

        public KubernetesDirectoryInformationProvider(ISystemLog log, ISilentProcessRunner silentProcessRunner)
        {
            this.log = log;
            this.silentProcessRunner = silentProcessRunner;
        }

        public ulong? GetPathUsedBytes(string directoryPath)
        {
            return directoryInformationCache.TryGetCache(out var totalBytes) ? totalBytes : GetDriveBytesUsingDu(directoryPath);
        }

        public ulong? GetPathTotalBytes()
        {
            return KubernetesUtilities.GetResourceBytes(KubernetesConfig.PersistentVolumeSize);
        }
        
        
        ulong? GetDriveBytesUsingDu(string directoryPath)
        {
            var stdOut = new List<string>();
            var stdErr = new List<string>();
            var exitCode = silentProcessRunner.ExecuteCommand("du", $"-s -B 1 {directoryPath}", "/", stdOut.Add, stdErr.Add);

            if (exitCode != 0)
            {
                log.Warn("Could not reliably get disk space using du. Getting best approximation...");
                log.Info($"Du stdout returned {stdOut}");
                log.Info($"Du stderr returned {stdErr}");
            }

            var lineWithDirectory = stdOut.LastOrDefault(outputLine => outputLine.Contains(directoryPath));

            if (!ulong.TryParse(lineWithDirectory?.Split('\t')[0], out var bytes)) return null;
            directoryInformationCache.SetCache(bytes);
            return bytes;
        }
    }
}