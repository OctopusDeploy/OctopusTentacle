using System;

namespace Octopus.Shared.Contracts
{
    public static class SpecialVariables
    {
        // Set by Octopus Server exclusively
        public static readonly string EnvironmentName = "OctopusEnvironmentName";
        public static readonly string EnvironmentId = "OctopusEnvironmentId";
        public static readonly string MachineName = "OctopusMachineName";
        public static readonly string PackageName = "OctopusPackageName";
        public static readonly string PackageVersion = "OctopusPackageVersion";
        public static readonly string PackageNameAndVersion = "OctopusPackageNameAndVersion";
        public static readonly string ProjectName = "OctopusProjectName";
        public static readonly string ProjectId = "OctopusProjectId";
        public static readonly string ForcePackageRedeployment = "OctopusForcePackageRedeployment";
        public static readonly string TaskId = "OctopusTaskId";
        public static readonly string ReleaseNumber = "OctopusReleaseNumber";
        public static readonly string RetentionPolicySet = "OctopusRetentionPolicySet";
        public static readonly string RetentionPolicyItemsToKeep = "OctopusRetentionPolicyItemsToKeep";
        public static readonly string RetentionPolicyDaysToKeep = "OctopusRetentionPolicyDaysToKeep";
        
        // Defaulted by Tentacle, but overridable by user
        public static readonly string TreatWarningsAsErrors = "OctopusTreatWarningsAsErrors";
        public static readonly string PackageDirectoryPath = "OctopusPackageDirectoryPath";
        public static readonly string PurgePackageDirectoryBeforeCopy = "OctopusPurgePackageDirectoryBeforeCopy";
        public static readonly string WebSiteName = "OctopusWebSiteName";
        public static readonly string NotAWebSite = "OctopusNotAWebSite";
        public static readonly string PrintVariables = "OctopusPrintVariables";
        public static readonly string PrintEvaluatedVariables = "OctopusPrintEvaluatedVariables";
        public static readonly string IgnoreMissingVariableTokens = "OctopusIgnoreMissingVariableTokens";
        public static readonly string NoVariableTokenReplacement = "OctopusNoVariableTokenReplacement";
        public static readonly string MaxParallelism = "OctopusMaxParallelism";
        public static readonly string UseLegacyIisSupport = "OctopusUseLegacyIisSupport";

        // Defaulted by Tentacle, overridable by user, but very advanced
        public static readonly string VariableTokenRegex = "OctopusVariableTokenRegex";

        // Set by Tentacle exclusively
        public static readonly string OriginalPackageDirectoryPath = "OctopusOriginalPackageDirectoryPath";
        public static readonly string LastErrorMessage = "OctopusLastErrorMessage";
        public static readonly string LastError = "OctopusLastError";

        public static string[] GetUserVariables()
        {
            return new[] { TreatWarningsAsErrors, PackageDirectoryPath, PurgePackageDirectoryBeforeCopy, WebSiteName, NotAWebSite, PrintVariables, PrintEvaluatedVariables, IgnoreMissingVariableTokens, NoVariableTokenReplacement, MaxParallelism, UseLegacyIisSupport };
        }
    }
}