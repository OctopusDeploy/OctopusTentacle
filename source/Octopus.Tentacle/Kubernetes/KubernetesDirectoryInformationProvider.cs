using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Client.Extensions;
using Microsoft.Extensions.Caching.Memory;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesDirectoryInformationProvider
    {
        public ulong? GetPathUsedBytes(string directoryPath);
        public ulong? GetPathTotalBytes();
    }
    
    public class KubernetesDirectoryInformationProvider : IKubernetesDirectoryInformationProvider
    {
        readonly ISystemLog log;
        readonly ISilentProcessRunner silentProcessRunner;
        readonly IMemoryCache directoryInformationCache;
        
        //30s gives us fairly up to date information, but doesn't impact performance too much.
        //For 50 concurrent Cloud deployments:
        //No cache: 30min ea.
        //Cache w/15s expiry: 15min ea.
        //Cache w/30s expiry: 11min ea.
        //Cache w/60s expiry: 9min ea.
        //No calls to `du` at all: 8min ea.
        static readonly TimeSpan CacheExpiry = TimeSpan.FromSeconds(30);

        public KubernetesDirectoryInformationProvider(ISystemLog log, ISilentProcessRunner silentProcessRunner, IMemoryCache directoryInformationCache)
        {
            this.log = log;
            this.silentProcessRunner = silentProcessRunner;
            this.directoryInformationCache = directoryInformationCache;
        }

        public ulong? GetPathUsedBytes(string directoryPath)
        {
            return directoryInformationCache.GetOrCreate(directoryPath, e =>
            {
                e.SetAbsoluteExpiration(CacheExpiry);
                return GetDriveBytesUsingDu(directoryPath);
            });
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
                log.Info($"Du stdout returned {stdOut.CommaSeparate()}");
                log.Info($"Du stderr returned {stdErr.CommaSeparate()}");
            }

            var lineWithDirectory = stdOut.LastOrDefault(outputLine => outputLine.Contains(directoryPath));

            if (ulong.TryParse(lineWithDirectory?.Split('\t')[0], out var bytes))
                return bytes;
            return null;
        }
    }
}