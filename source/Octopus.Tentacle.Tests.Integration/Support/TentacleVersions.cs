using System;

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

        // First version with ScriptServiceV2
        public static readonly Version v7_0_1_ScriptServiceV2Added = new("7.0.1");

        // Last version without ScriptServiceV3Alpha
        public static readonly Version v8_0_34_LastWithoutScriptServiceV3Alpha = new("8.0.34");

        // The version compiled from the current source
        public static readonly Version? Current = null;

        public static Version[] AllTestedVersionsToDownload =
        {
            v5_0_4_FirstLinuxRelease,
            v5_0_12_AutofacServiceFactoryIsInShared,
            v5_0_15_LastOfVersion5,
            v6_3_417_LastWithScriptServiceV1Only,
            v6_3_451_NoCapabilitiesService,
            v8_0_34_LastWithoutScriptServiceV3Alpha
        };
    }

    public static class VersionExtensionMethods
    {
        public static bool HasScriptServiceV3Alpha(this Version? version)
        {
            return version == TentacleVersions.Current || version > TentacleVersions.v8_0_34_LastWithoutScriptServiceV3Alpha;
        }
        public static bool HasScriptServiceV2(this Version? version)
        {
            return version == TentacleVersions.Current || version >= TentacleVersions.v7_0_1_ScriptServiceV2Added;
        }
    }
}