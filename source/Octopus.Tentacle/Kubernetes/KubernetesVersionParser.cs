using System;
using System.Text.RegularExpressions;
using k8s.Models;

namespace Octopus.Tentacle.Kubernetes
{
    public static class KubernetesVersionParser
    {
        public static ClusterVersion ParseClusterVersion(VersionInfo versionInfo)
        {
            return new ClusterVersion(SanitizeAndParseVersionNumber(versionInfo.Major), SanitizeAndParseVersionNumber(versionInfo.Minor));
        }
        
        static int SanitizeAndParseVersionNumber(string version)
        {
            if (int.TryParse(version, out var result))
                return result;
            
            var sanitized = Regex.Replace(version, "[^0-9]", "");

            return int.Parse(sanitized);
        }

    }
    public record ClusterVersion(int Major, int Minor);
}