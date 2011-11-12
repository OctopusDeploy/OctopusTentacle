using System;

namespace Octopus.Shared.Contracts
{
    public static class SpecialVariables
    {
        // Set by Octopus
        public static readonly string EnvironmentName = "OctopusEnvironmentName";
        
        public static readonly string PackageName = "OctopusPackageName"; 
        public static readonly string PackageVersion = "OctopusPackageVersion";
        public static readonly string PackageNameAndVersion = "OctopusPackageNameAndVersion";
        public static readonly string DateTimeToday = "OctopusDateTimeToday";
        public static readonly string WebSiteName = "OctopusWebSiteName";
        public static readonly string MachineName = "OctopusMachineName";
        public static readonly string PackageDirectoryPath = "OctopusPackageDirectoryPath";
    }
}