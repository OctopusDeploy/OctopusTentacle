using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Octopus.Tentacle.CommonTestUtils;

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

        public static Version[] AllTestedVersionsToDownload = GetAllTestedVersionsToDownload();

        public static readonly Version[] VersionsUnsupportedByCurrentOperatingSystemAndArchitecture = GetVersionsUnsupportedByCurrentOperatingSystemAndArchitecture();

        static Version[] GetVersionsUnsupportedByCurrentOperatingSystemAndArchitecture()
        {
            if (!PlatformDetection.IsRunningOnMac && RuntimeInformation.ProcessArchitecture != Architecture.Arm64) return Array.Empty<Version>();

            if (PlatformDetection.IsRunningOnWindows) return Array.Empty<Version>(); 

            // These versions are not available on MacOS or ARM
            return new []
            {
                v5_0_4_FirstLinuxRelease,
                v5_0_12_AutofacServiceFactoryIsInShared,
                v5_0_15_LastOfVersion5
            };
        }
        static Version[] GetAllTestedVersionsToDownload()
        {
            var versions = new List<Version>();

            if (PlatformDetection.IsRunningOnWindows || (!PlatformDetection.IsRunningOnMac && RuntimeInformation.ProcessArchitecture != Architecture.Arm64))
            {
                // These versions are not available on MacOS or ARM
                versions.Add(v5_0_4_FirstLinuxRelease);
                versions.Add(v5_0_12_AutofacServiceFactoryIsInShared);
                versions.Add(v5_0_15_LastOfVersion5);
            }

            versions.Add(v6_3_417_LastWithScriptServiceV1Only);
            versions.Add(v6_3_451_NoCapabilitiesService);
            versions.Add(v7_1_189_SyncHalibutAndScriptServiceV2);
            versions.Add(v8_0_81_AsyncHalibutAndLastWithoutScriptServiceV3Alpha);

            return versions.ToArray();
        }
    }

    public static class VersionExtensionMethods
    {
        public static bool HasScriptServiceV3Alpha(this Version? version)
        {
            return version == TentacleVersions.Current || version > TentacleVersions.v8_0_81_AsyncHalibutAndLastWithoutScriptServiceV3Alpha;
        }

        public static bool HasScriptServiceV2(this Version? version)
        {
            return version == TentacleVersions.Current || version >= TentacleVersions.v7_1_189_SyncHalibutAndScriptServiceV2;
        }
    }
}