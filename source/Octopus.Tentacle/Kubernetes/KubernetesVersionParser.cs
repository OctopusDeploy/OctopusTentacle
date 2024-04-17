using System;
using System.Text.RegularExpressions;
using k8s.Models;

namespace Octopus.Tentacle.Kubernetes
{
    public static class KubernetesVersionParser
    {
        static Regex clusterVersionRegex = new Regex("[^0-9]");
        
        public static ClusterVersion ParseClusterVersion(VersionInfo versionInfo)
        {
            return new ClusterVersion(SanitizeAndParseVersionNumber(versionInfo.Major), SanitizeAndParseVersionNumber(versionInfo.Minor));
        }
        
        static int SanitizeAndParseVersionNumber(string version)
        {
            return int.Parse(Regex.Replace(version, "[^0-9]", ""));
        }

    }
    public record ClusterVersion(int Major, int Minor);
}