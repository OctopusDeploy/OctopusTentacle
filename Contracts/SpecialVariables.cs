using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Octopus.Shared.Contracts
{
    public static class SpecialVariables
    {
        static readonly Dictionary<string, string> LegacyToCurrent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, string> CurrentToLegacy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static SpecialVariables()
        {
        }

        // Set by Octopus Server exclusively
        public static readonly string RetentionPolicySet = "OctopusRetentionPolicySet";
        public static readonly string RetentionPolicyItemsToKeep = "OctopusRetentionPolicyItemsToKeep";
        public static readonly string RetentionPolicyDaysToKeep = "OctopusRetentionPolicyDaysToKeep";

        // Defaulted by Tentacle, but overridable by user
        public static readonly string TreatWarningsAsErrors = "OctopusTreatWarningsAsErrors";
        public static readonly string PrintVariables = "OctopusPrintVariables";
        public static readonly string PrintEvaluatedVariables = "OctopusPrintEvaluatedVariables";
        public static readonly string IgnoreMissingVariableTokens = "OctopusIgnoreMissingVariableTokens";
        public static readonly string NoVariableTokenReplacement = "OctopusNoVariableTokenReplacement";
        public static readonly string MaxParallelism = "OctopusMaxParallelism";
        public static readonly string UseLegacyIisSupport = "OctopusUseLegacyIisSupport";
        public static readonly string UseLegacyPowerShellEngine = "OctopusUseLegacyPowerShellEngine";
        
        // Defaulted by Tentacle, overridable by user, but very advanced
        public static readonly string VariableTokenRegex = "OctopusVariableTokenRegex";

        // Set by Tentacle exclusively
        public static readonly string OriginalPackageDirectoryPath = "OctopusOriginalPackageDirectoryPath";
        public static readonly string LastErrorMessage = "OctopusLastErrorMessage";
        public static readonly string LastError = "OctopusLastError";

        public static string[] GetUserVariables()
        {
            return new[] { TreatWarningsAsErrors, PrintVariables, PrintEvaluatedVariables, IgnoreMissingVariableTokens, NoVariableTokenReplacement, MaxParallelism, UseLegacyIisSupport };
        }

        public static readonly string SecretSuffix = "[Secret]";
        public static readonly string NoSubstituionSuffix = "[NoSubstitute]";

        public static bool IsSecret(string variableName)
        {
            return variableName.Contains(SecretSuffix);
        }

        public static bool AllowsSubstitution(string variableName)
        {
            return !variableName.Contains(NoSubstituionSuffix);
        }

        public static class Environment
        {
            public static readonly string Id = "Octopus.Environment.Id";
            public static readonly string Name = "Octopus.Environment.Name";
            public static readonly string SortOrder = "Octopus.Environment.SortOrder";
        }

        public static class Machine
        {
            public static readonly string Id = "Octopus.Machine.Id";
            public static readonly string Name = "Octopus.Machine.Name";
        }

        public static class Release
        {
            public static readonly string Number = "Octopus.Release.Number";
            public static readonly string Notes = "Octopus.Release.Notes";
        }

        public static class Deployment
        {
            public static readonly string Id = "Octopus.Deployment.Id";
            public static readonly string Name = "Octopus.Deployment.Id";
            public static readonly string Comments = "Octopus.Deployment.Comments";
            public static readonly string ForcePackageRedeployment = "Octopus.Deployment.ForcePackageRedeployment";
            public static readonly string ForcePackageDownload = "Octopus.Deployment.ForcePackageDownload";
        }

        public static class Project
        {
            public static readonly string Id = "Octopus.Project.Id";
            public static readonly string Name = "Octopus.Project.Name";
        }

        public static class Task
        {
            public static readonly string Id = "Octopus.Task.Id";
            public static readonly string Name = "Octopus.Task.Name";
            public static readonly string Arguments = "Octopus.Task.Arguments";
        }

        public static class Web
        {
            public static readonly string ProjectLink = "Octopus.Web.ProjectLink";
            public static readonly string ReleaseLink = "Octopus.Web.ReleaseLink";
            public static readonly string DeploymentLink = "Octopus.Web.DeploymentLink";
        }

        public static class Step
        {
            public static readonly string Id = "Octopus.Step.Id";
            public static readonly string Name = "Octopus.Step.Name";
            public static readonly string LogicalId = "Octopus.Step.LogicalId";
            
            public static readonly string IsTentacleDeployment = "Octopus.Step.IsTentacleDeployment";
            public static readonly string IsFtpDeployment = "Octopus.Step.IsFtpDeployment";
            public static readonly string IsAzureDeployment = "Octopus.Step.IsAzureDeployment";

            public static readonly string TargetRoles = "Octopus.Step.TargetRoles";
            
            public static class Package
            {
                public static readonly string NuGetPackageId = "Octopus.Step.Package.NuGetPackageId";
                public static readonly string NuGetPackageVersion = "Octopus.Step.Package.NuGetPackageVersion";

                public static readonly string ShouldDownloadOnTentacle = "Octopus.Step.Package.DownloadOnTentacle";
                public static readonly string NuGetFeedId = "Octopus.Step.Package.NuGetFeedId";
                
                public static readonly string UpdateIisWebsite = "Octopus.Step.Package.UpdateIisWebsite";
                public static readonly string UpdateIisWebsiteName = "Octopus.Step.Package.UpdateIisWebsiteName";

                public static readonly string CustomInstallationDirectory = "Octopus.Step.Package.CustomInstallationDirectory";
                public static readonly string CustomInstallationDirectoryShouldBePurgedBeforeDeployment = "Octopus.Step.Package.CustomInstallationDirectoryShouldBePurgedBeforeDeployment";
                
                public static readonly string AutomaticallyUpdateAppSettingsAndConnectionStrings = "Octopus.Step.Package.AutomaticallyUpdateAppSettingsAndConnectionStrings";
                public static readonly string AutomaticallyRunConfigurationTransformationFiles = "Octopus.Step.Package.AutomaticallyRunConfigurationTransformationFiles";
                public static readonly string IgnoreConfigTranformationErrors = "Octopus.Step.Package.IgnoreConfigTranformationErrors";
                public static readonly string AdditionalXmlConfigurationTransforms = "Octopus.Step.Package.AdditionalXmlConfigurationTransforms";
            }

            public static class Ftp
            {
                public static readonly string Host = "Octopus.Step.Ftp.Host";
                public static readonly string Username = "Octopus.Step.Ftp.Username";
                public static readonly string Password = "Octopus.Step.Ftp.Password" + SecretSuffix + NoSubstituionSuffix;
                public static readonly string UseFtps = "Octopus.Step.Ftp.UseFtps";
                public static readonly string FtpPort = "Octopus.Step.Ftp.FtpPort";
                public static readonly string RootDirectory = "Octopus.Step.Ftp.RootDirectory";
                public static readonly string DeleteDestinationFiles = "Octopus.Step.Ftp.DeleteDestinationFiles";
            }

            public static class Email
            {
                public static readonly string To = "Octopus.Step.Email.To";
                public static readonly string Cc = "Octopus.Step.Email.Cc";
                public static readonly string Bcc = "Octopus.Step.Email.Bcc";
                public static readonly string IsHtml = "Octopus.Step.Email.IsHtml";
                public static readonly string Subject = "Octopus.Step.Email.Subject";
                public static readonly string Body = "Octopus.Step.Email.Body";
            }

            public static class Script
            {
                public static readonly string ScriptBody = "Octopus.Step.Script.ScriptBody";
            }

            public static class Azure
            {
                public static readonly string SubscriptionId = "Octopus.Step.Azure.SubscriptionId";
                public static readonly string Endpoint = "Octopus.Step.Azure.Endpoint";
                public static readonly string StorageAccountName = "Octopus.Step.Azure.StorageAccountName";
                public static readonly string CloudServiceName = "Octopus.Step.Azure.CloudServiceName";
                public static readonly string UploadedPackageUri = "Octopus.Step.Azure.UploadedPackageUri";
                public static readonly string Slot = "Octopus.Step.Azure.Slot";
                public static readonly string SwapIfPossible = "Octopus.Step.Azure.SwapIfPossible";
                public static readonly string UseCurrentInstanceCount = "Octopus.Step.Azure.UseCurrentInstanceCount";
            }

            public static class Manual
            {
                public static readonly string Instructions = "Octopus.Step.Manual.Instructions";
            }
        }

        public static IEnumerable<string> GetAllUsableByUser()
        {
            return GetAll(typeof(SpecialVariables), (name, value) => !name.StartsWith("Legacy")).OrderBy(o => o).ToList();
        }

        public static IEnumerable<string> GetAllLegacy()
        {
            return GetAll(typeof(SpecialVariables), (name, value) => name.StartsWith("Legacy")).OrderBy(o => o).ToList();
        }

        static void AddLegacy(string legacyVariableName, string currentVariableName)
        {
            LegacyToCurrent[legacyVariableName] = currentVariableName;
            CurrentToLegacy[currentVariableName] = legacyVariableName;
        }

        public static IEnumerable<string> GetAlternativeNames(string name)
        {
            var names = new List<string>();
            names.Add(name);
            string value;
            if (LegacyToCurrent.TryGetValue(name, out value))
                names.Add(value);
            if (CurrentToLegacy.TryGetValue(name, out value))
                names.Add(value);

            return names.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        static IEnumerable<string> GetAll(Type rootType, Func<string, string, bool> filter)
        {
            foreach (var member in rootType.GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                var value = (string) member.GetValue(null);
                if (value.StartsWith("["))
                    continue;

                if (filter(member.Name, value))
                {
                    yield return value;
                }
            }

            foreach (var type in rootType.GetNestedTypes())
            {
                foreach (var item in GetAll(type, filter))
                {
                    yield return item;
                }
            }
        }
    }
}