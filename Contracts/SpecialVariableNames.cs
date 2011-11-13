using System;

namespace Octopus.Shared.Contracts
{
    public static class SpecialVariables
    {
        // Set by Octopus Server exclusively
        public static readonly string EnvironmentName = "OctopusEnvironmentName";
        public static readonly string MachineName = "OctopusMachineName";
        public static readonly string PackageName = "OctopusPackageName";
        public static readonly string PackageVersion = "OctopusPackageVersion";
        public static readonly string PackageNameAndVersion = "OctopusPackageNameAndVersion";
        public static readonly string ProjectName = "OctopusProjectName";
        public static readonly string ForcePackageRedeployment = "OctopusForcePackageRedeployment";
        
        // Defaulted by Tentacle, but overridable by user
        public static readonly string PackageDirectoryPath = "OctopusPackageDirectoryPath";
        public static readonly string PurgePackageDirectoryBeforeCopy = "OctopusPurgePackageDirectoryBeforeCopy";
        public static readonly string WebSiteName = "OctopusWebSiteName";
    }
}