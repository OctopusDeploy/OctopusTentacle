using System.Runtime.InteropServices;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public static class TentacleVersions
    {
        // First linux Release 9/9/2019
        public static readonly Version v5_0_4_FirstLinuxRelease = new("5.0.4");

        public static readonly Version v5_0_12_AutofacServiceFactoryIsInShared = new("5.0.12");

        public static readonly Version v5_0_15_LastOfVersion5 = new("5.0.15");

        // Before script service v2
        // Does not contain the capabilities v2 service.
        // Latest support release with only script service v1
        public static readonly Version v6_3_417_LastWithScriptServiceV1Only = new("6.3.417");

        // No capabilities service
        public static Version v6_3_451_NoCapabilitiesService = new("6.3.451");

        // Last version of v7 with ScriptServiceV2
        public static readonly Version v7_1_189_SyncHalibutAndScriptServiceV2 = new("7.1.189");

        // Last version without ScriptServiceV3Alpha
        // Contains ScriptServiceV1 and ScriptServiceV2
        public static readonly Version v8_0_81_AsyncHalibutAndLastWithoutScriptServiceV3Alpha = new("8.0.81");

        // The version compiled from the current source
        public static readonly Version? Current = null;

        static readonly IReadOnlyList<Version> Version5Releases = new[]
        {
            v5_0_4_FirstLinuxRelease,
            v5_0_12_AutofacServiceFactoryIsInShared,
            v5_0_15_LastOfVersion5
        };

        // Tests whether Tentacle version 5 releases can actually be run on this host.
        static readonly bool Version5IsRunnableHere = IsVersion5RunnableHere();

        public static Version[] AllTestedVersionsToDownload = GetAllTestedVersionsToDownload();

        public static readonly IReadOnlyList<Version> VersionsUnsupportedByCurrentOperatingSystemAndArchitecture = GetVersionsUnsupportedByCurrentOperatingSystemAndArchitecture();

        static bool IsVersion5RunnableHere()
        {
            if (PlatformDetection.IsRunningOnWindows) return true;
            if (PlatformDetection.IsRunningOnMac) return false;
            if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64) return false;

            return !PlatformDetection.IsLinuxWithoutOpenSsl1x;
        }

        static IReadOnlyList<Version> GetVersionsUnsupportedByCurrentOperatingSystemAndArchitecture()
            => Version5IsRunnableHere ? Array.Empty<Version>() : Version5Releases;

        static Version[] GetAllTestedVersionsToDownload()
        {
            var versions = new List<Version>();

            if (Version5IsRunnableHere) versions.AddRange(Version5Releases);

            versions.Add(v6_3_417_LastWithScriptServiceV1Only);
            versions.Add(v6_3_451_NoCapabilitiesService);
            versions.Add(v7_1_189_SyncHalibutAndScriptServiceV2);
            versions.Add(v8_0_81_AsyncHalibutAndLastWithoutScriptServiceV3Alpha);

            return versions.ToArray();
        }
    }

    public static class VersionExtensionMethods
    {
        public static bool HasScriptServiceV2(this Version? version)
        {
            return version == TentacleVersions.Current || version >= TentacleVersions.v7_1_189_SyncHalibutAndScriptServiceV2;
        }
    }
}