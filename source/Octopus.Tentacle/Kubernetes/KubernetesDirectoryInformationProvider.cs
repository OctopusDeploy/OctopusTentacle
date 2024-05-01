using System.Collections.Generic;
using System.Linq;
using Octopus.Client.Extensions;
using Octopus.Diagnostics;
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

        public KubernetesDirectoryInformationProvider(ISystemLog log, ISilentProcessRunner silentProcessRunner)
        {
            this.log = log;
            this.silentProcessRunner = silentProcessRunner;
        }

        public ulong? GetPathUsedBytes(string directoryPath)
        {
            return GetDriveBytesUsingDu(directoryPath);
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