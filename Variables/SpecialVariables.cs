using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace Octopus.Shared.Variables
{
    // ReSharper disable MemberHidesStaticFromOuterClass

    public static class SpecialVariables
    {
        // Set by Octopus Server exclusively
        [Define(Category = VariableCategory.Hidden)] public static readonly string RetentionPolicySet = "OctopusRetentionPolicySet";
        [Define(Category = VariableCategory.Hidden)] public static readonly string RetentionPolicyItemsToKeep = "OctopusRetentionPolicyItemsToKeep";
        [Define(Category = VariableCategory.Hidden)] public static readonly string RetentionPolicyDaysToKeep = "OctopusRetentionPolicyDaysToKeep";
        [Define(Category = VariableCategory.Hidden)] public static readonly string FailureEncountered = "OctopusFailureEncountered";
        // Defaulted by Tentacle, but overridable by user
        [Define(Category = VariableCategory.Hidden)] public static readonly string TreatWarningsAsErrors = "OctopusTreatWarningsAsErrors";
        [Define(Category = VariableCategory.Hidden)] public static readonly string PrintVariables = "OctopusPrintVariables";
        [Define(Category = VariableCategory.Hidden)] public static readonly string PrintEvaluatedVariables = "OctopusPrintEvaluatedVariables";
        [Define(Category = VariableCategory.Hidden)] public static readonly string IgnoreMissingVariableTokens = "OctopusIgnoreMissingVariableTokens";
        [Define(Category = VariableCategory.Hidden)] public static readonly string UseLegacyIisSupport = "OctopusUseLegacyIisSupport";
        [Define(Category = VariableCategory.Hidden)] public static readonly string AllowInteractivePowerShell = "OctopusAllowInteractivePowerShell";
        [Define(Category = VariableCategory.Hidden)] public static readonly string BypassDeploymentMutex = "OctopusBypassDeploymentMutex";
        public static readonly string UpdateEnvironmentVariables = "UpdateEnvironmentVariables";
        // Set by Octopus Server to DeploymentEnvironment.UseGuidedFailure || DeploymentUseGuidedFailure,
        // but overridable by user
        [Define(Category = VariableCategory.Hidden)] public static readonly string UseGuidedFailure = "OctopusUseGuidedFailure";
        // Set by Tentacle exclusively
        [Define(Category = VariableCategory.Hidden)] public static readonly string OriginalPackageDirectoryPath = "OctopusOriginalPackageDirectoryPath";
        [Define(Category = VariableCategory.Hidden)] public static readonly string SearchForScriptsRecursively = "OctopusSearchForScriptsRecursively";
        [Define(Category = VariableCategory.Hidden)] public static readonly string LastErrorMessage = "OctopusLastErrorMessage";
        [Define(Category = VariableCategory.Hidden)] public static readonly string LastError = "OctopusLastError";
        [Define(Category = VariableCategory.Hidden)] public static readonly string NewWorkingDirectoryPath = "OctopusNewWorkingDirectoryPath";
        [Define(Category = VariableCategory.Hidden)] public static readonly string AppliedXmlConfigTransforms = "OctopusAppliedXmlConfigTransforms";
        [Define(Category = VariableCategory.Hidden)] public static readonly string SuppressNestedScriptWarning = "OctopusSuppressNestedScriptWarning";
        [Define(Category = VariableCategory.Hidden)] public static readonly string DeleteScriptsOnCleanup = "OctopusDeleteScriptsOnCleanup";
        [Define(Category = VariableCategory.Hidden)] public static readonly string StagedPackageHash = "StagedPackage.Hash";
        [Define(Category = VariableCategory.Hidden)] public static readonly string StagedPackageSize = "StagedPackage.Size";
        [Define(Category = VariableCategory.Hidden)] public static readonly string StagedPackageFullPathOnRemoteMachine = "StagedPackage.FullPathOnRemoteMachine";
        [Define(Category = VariableCategory.Hidden)] public static readonly string HasLatestCalamariVersion = "HasLatestCalamariVersion";

        static SpecialVariables()
        {
            Definitions = new Lazy<ReadOnlyCollection<VariableDefinition>>(() => GetDefinitions(typeof (SpecialVariables)).OrderBy(o => o.Name).ToList().AsReadOnly());
        }

        public static Lazy<ReadOnlyCollection<VariableDefinition>> Definitions { get; private set; }

        public static bool IsActionVariable(string variableName)
        {
            return IsElementOf("Octopus.Action", variableName);
        }

        public static bool IsStepVariable(string variableName)
        {
            return IsElementOf("Octopus.Step", variableName);
        }

        public static bool IsElementOf(string indexedCollectionName, string variableName)
        {
            return variableName.StartsWith(indexedCollectionName + ".");
        }

        public static bool AppliesToActionType(string actionType, string variableName)
        {
            if (actionType == "Octopus.Manual")
            {
                return variableName.Contains(".Manual.");
            }
            if (actionType == "Octopus.Email")
            {
                return variableName.Contains(".Email.");
            }
            if (actionType == "Octopus.TentaclePackage")
            {
                return variableName.Contains(".Package.");
            }
            if (actionType == "Octopus.Script")
            {
                return variableName.Contains(".Script.");
            }
            return false;
        }

        public static string IndexActionVariableByKey(string variableName, string key)
        {
            return !IsActionVariable(variableName)
                ? variableName
                : variableName.Replace("Octopus.Action.", "Octopus.Action[" + key + "].");
        }

        public static string IndexStepVariableByKey(string variableName, string key)
        {
            return variableName.Replace("Octopus.Step.", "Octopus.Step[" + key + "].");
        }

        public static bool IsLibraryScriptModule(string variableName)
        {
            return variableName.StartsWith("Octopus.Script.Module[");
        }

        public static List<string> GetUserVisibleVariables()
        {
            return Definitions.Value.Where(d => d.Category != VariableCategory.Hidden).Select(v => v.Name).ToList();
        }

        public static string GetLibraryScriptModuleName(string variableName)
        {
            return variableName.Replace("Octopus.Script.Module[", "").TrimEnd(']');
        }

        static IEnumerable<VariableDefinition> GetDefinitions(Type rootType)
        {
            foreach (var member in rootType.GetMembers(BindingFlags.Static | BindingFlags.Public))
            {
                var ca = member.GetCustomAttributes(typeof (DefineAttribute), false).OfType<DefineAttribute>().FirstOrDefault();
                if (ca == null)
                    continue;

                var name = ca.Pattern ?? (string)((FieldInfo)member).GetValue(null);
                var description = ca.Description;
                var example = ca.Example;
                if (description == null && ca.Category != VariableCategory.Hidden)
                {
                    var type = name.Split('.').Reverse().Skip(1).First().ToLowerInvariant();
                    if (name.EndsWith("Id"))
                    {
                        description = string.Format("The ID of the {0}", type);
                        if (example == null)
                            example = string.Format("{0}s-123", type);
                    }
                    else if (name.EndsWith("Name"))
                    {
                        description = string.Format("The name of the {0}", type);
                    }
                    else
                    {
                        throw new Exception("Variable is missing a description: " + name);
                    }
                }

                if (example == null && ca.Category != VariableCategory.Hidden)
                    throw new Exception("Variable is missing an example: " + name);

                yield return new VariableDefinition(name, description, example, ca.Pattern != null, ca.Domain, ca.Category);
            }

            foreach (var type in rootType.GetNestedTypes())
            {
                foreach (var item in GetDefinitions(type))
                {
                    yield return item;
                }
            }
        }

        public static bool IsExcludedFromLocalVariables(string name)
        {
            return name.Contains("[");
        }

        public static bool IsPrintable(string name)
        {
            return !name.Contains("CustomScripts.");
        }

        public static class Acquire
        {
            [Define(Description = "Controls the number of package acquisitions that will be allowed to run concurrently.", Example = "2")] public static readonly string MaxParallelism = "Octopus.Acquire.MaxParallelism";
            [Define(Description = "Toggle whether delta compression will be enabled when sending packages to targets.", Example = "false")] public static readonly string DeltaCompressionEnabled = "Octopus.Acquire.DeltaCompressionEnabled";
        }

        public static class Environment
        {
            [Define] public static readonly string Id = "Octopus.Environment.Id";
            [Define(Example = "Production")] public static readonly string Name = "Octopus.Environment.Name";
            [Define(Description = "The ordering applied to the environment when it is displayed on the dashboard and elsewhere", Example = "3")] public static readonly string SortOrder = "Octopus.Environment.SortOrder";

            [Define(Pattern = "Octopus.Environment.MachinesInRole[_role_]", Description = "Lists the machines in a specified role", Example = "WEBSRV01,WEBSRV02")]
            public static string MachinesInRole(string role)
            {
                return "Octopus.Environment.MachinesInRole[" + role + "]";
            }
        }

        public static class Machine
        {
            [Define] public static readonly string Id = "Octopus.Machine.Id";
            [Define(Example = "WEBSVR01")] public static readonly string Name = "Octopus.Machine.Name";
            [Define(Description = "The roles applied to the machine", Example = "web-server,frontend", Domain = VariableDomain.List)] public static readonly string Roles = "Octopus.Machine.Roles";
            public static readonly string Hostname = "Octopus.Machine.Hostname";
            public static readonly string CommunicationStyle = "Octopus.Machine.CommunicationStyle";
        }

        public static class Account
        {
            [Define(Description = "The name of the account", Example = "OctopusDeployAdmin")] public static readonly string Name = "Octopus.Account.Name";
            [Define(Description = "The account-type", Example = "UsernamePassword")] public static readonly string AccountType = "Octopus.Account.AccountType";
        }

        public static class Release
        {
            [Define(Description = "The version number of the release", Example = "1.2.3")] public static readonly string Number = "Octopus.Release.Number";
            [Define(Description = "Release notes associated with the release, in Markdown format", Example = "Fixes bugs 1, 2 & 3")] public static readonly string Notes = "Octopus.Release.Notes";

            public static class Previous
            {
                [Define(Description = "The ID of the last release of the project", Example = "releases-122")] public static string Id = "Octopus.Release.Previous.Id";
                [Define(Description = "The version number of the last release of the project", Example = "1.2.2")] public static string Number = "Octopus.Release.Previous.Number";
            }

            public static class PreviousForEnvironment
            {
                [Define(Description = "The ID of the last release of the project to the current environment", Example = "releases-112")] public static string Id = "Octopus.Release.PreviousForEnvironment.Id";
                [Define(Description = "The version number of the last release of the project to the current environment", Example = "1.1.2")] public static string Number = "Octopus.Release.PreviousForEnvironment.Number";
            }
        }

        public static class Version
        {
            public static readonly string LastMajor = "Octopus.Version.LastMajor";
            public static readonly string LastMinor = "Octopus.Version.LastMinor";
            public static readonly string LastPatch = "Octopus.Version.LastPatch";
            public static readonly string LastBuild = "Octopus.Version.LastBuild";
            public static readonly string LastRevision = "Octopus.Version.LastRevision";
            public static readonly string LastSuffix = "Octopus.Version.LastSuffix";
            public static readonly string NextMajor = "Octopus.Version.NextMajor";
            public static readonly string NextMinor = "Octopus.Version.NextMinor";
            public static readonly string NextPatch = "Octopus.Version.NextPatch";
            public static readonly string NextBuild = "Octopus.Version.NextBuild";
            public static readonly string NextRevision = "Octopus.Version.NextRevision";
            public static readonly string NextSuffix = "Octopus.Version.NextSuffix";
        }

        public static class Date
        {
            public static readonly string Year = "Octopus.Date.Year";
            public static readonly string Month = "Octopus.Date.Month";
            public static readonly string Day = "Octopus.Date.Day";
            public static readonly string DayOfYear = "Octopus.Date.DayOfYear";
        }

        public static class Time
        {
            public static readonly string Hour = "Octopus.Time.Hour";
            public static readonly string Minute = "Octopus.Time.Minute";
            public static readonly string Second = "Octopus.Time.Second";
        }

        public static class Deployment
        {
            [Define] public static readonly string Id = "Octopus.Deployment.Id";
            [Define(Example = "Deploy to Production")] public static readonly string Name = "Octopus.Deployment.Name";
            [Define(Description = "User-provided comments on the deployment", Example = "Signed off by Alice")] public static readonly string Comments = "Octopus.Deployment.Comments";
            [Define(Description = "If true, the package will be freshly downloaded from the feed/repository regardless of whether it is already present on the endpoint", Example = "False", Domain = VariableDomain.Boolean)] public static readonly string ForcePackageDownload = "Octopus.Deployment.ForcePackageDownload";
            [Define(Description = "Specific machines being targeted by the deployment, if any", Example = "machines-123,machines-124", Domain = VariableDomain.List)] public static readonly string SpecificMachines = "Octopus.Deployment.SpecificMachines";
            public static readonly string Machines = "Octopus.Deployment.Machines";
            [Define(Description = "The date and time at which the deployment was created", Example = "Tuesday 10th September 1:23 PM")] public static readonly string Created = "Octopus.Deployment.Created";
            [Define(Description = "The error causing the deployment to fail, if any", Example = "Script returned exit code 123")] public static readonly string Error = "Octopus.Deployment.Error";
            [Define(Description = "A detailed description of the error causing the deployment to fail", Example = "System.IO.FileNotFoundException: file C:\\Missing.txt does not exist (at...)")] public static readonly string ErrorDetail = "Octopus.Deployment.ErrorDetail";

            public static class CreatedBy
            {
                [Define(Description = "The ID of the user who initiated the deployment", Example = "users-123")] public static readonly string Id = "Octopus.Deployment.CreatedBy.Id";
                [Define(Description = "The username of the user who initiated the deployment", Example = "alice")] public static readonly string Username = "Octopus.Deployment.CreatedBy.Username";
                [Define(Description = "The full name of the user who initiated the deployment", Example = "Alice King")] public static readonly string DisplayName = "Octopus.Deployment.CreatedBy.DisplayName";
                [Define(Description = "The email address of the user who initiated the deployment", Example = "alice@example.com")] public static readonly string EmailAddress = "Octopus.Deployment.CreatedBy.EmailAddress";
            }

            public static class PreviousSuccessful
            {
                [Define(Description = "The ID of the previous successful deployment of this project in the target environment", Example = "deployments-122")] public static string Id = "Octopus.Deployment.PreviousSuccessful.Id";
            }
        }

        public static class Project
        {
            [Define] public static readonly string Id = "Octopus.Project.Id";
            [Define(Example = "OctoFx")] public static readonly string Name = "Octopus.Project.Name";
        }

        public static class ProjectGroup
        {
            [Define] public static readonly string Id = "Octopus.ProjectGroup.Id";
            [Define(Example = "Public Web Properties")] public static readonly string Name = "Octopus.ProjectGroup.Name";
        }

        public static class ServerTask
        {
            [Define(Example = "servertasks-123")] public static readonly string Id = "Octopus.Task.Id";
            [Define(Example = "Deploy release 1.2.3 to Production")] public static readonly string Name = "Octopus.Task.Name";

            [Define(Pattern = "Octopus.Task.Argument[_name_]", Description = "Argument values provided when creating the task", Example = "deployments-123")]
            public static string Argument(string key)
            {
                return "Octopus.Task.Argument[" + key + "]";
            }
        }

        public static class Web
        {
            [Define(Category = VariableCategory.Server, Description = "The default URL at which the server can be accessed", Example = "https://my-octopus")] public static readonly string BaseUrl = "Octopus.Web.BaseUrl";
            [Define(Description = "A path relative to `Octopus.Web.BaseUrl` at which the project can be viewed", Example = "/app/projects/projects-123")] public static readonly string ProjectLink = "Octopus.Web.ProjectLink";
            [Define(Description = "A path relative to `Octopus.Web.BaseUrl` at which the release can be viewed", Example = "/app/releases/releases-123")] public static readonly string ReleaseLink = "Octopus.Web.ReleaseLink";
            [Define(Description = "A path relative to `Octopus.Web.BaseUrl` at which the deployment can be viewed", Example = "/app/deployment/deployments-123")] public static readonly string DeploymentLink = "Octopus.Web.DeploymentLink";
        }

        public static class Step
        {
            [Define(Category = VariableCategory.Step, Example = "80b3ad09-eedf-40d6-9b66-cf97f5c0ffee")] public static readonly string Id = "Octopus.Step.Id";
            [Define(Category = VariableCategory.Step, Example = "Website")] public static readonly string Name = "Octopus.Step.Name";
            [Define(Category = VariableCategory.Step, Description = "The number of the step", Example = "2", Domain = VariableDomain.Number)] public static readonly string Number = "Octopus.Step.Number";

            public static class Status
            {
                [Define(Category = VariableCategory.Step, Description = "A code describing the current status of the step", Example = "Succeeded")] public static readonly string Code = "Octopus.Step.Status.Code";
                [Define(Category = VariableCategory.Step, Description = "If the step failed because of an error, a description of the error", Example = "The server could not be contacted")] public static readonly string Error = "Octopus.Step.Status.Error";
                [Define(Category = VariableCategory.Step, Description = "If the step failed because of an error, a full description of the error", Example = "System.Net.SocketException: The server could not be contacted (at ...)")] public static readonly string ErrorDetail = "Octopus.Step.Status.ErrorDetail";
            }
        }

        public static class Tentacle
        {
            public static class CurrentDeployment
            {
                [Define(Description = "The path to the package file being deployed", Example = "C:\\Octopus\\Tentacle\\Packages\\OctoFx.1.2.3.nupkg")] public static readonly string PackageFilePath = "Octopus.Tentacle.CurrentDeployment.PackageFilePath";
                [Define(Category = VariableCategory.Hidden)] public static readonly string RetentionPolicySubset = "Octopus.Tentacle.CurrentDeployment.RetentionPolicySubset";
                [Define(Description = "The intersection of the roles targeted by the step, and those applied to the machine", Example = "web-server")] public static readonly string TargetedRoles = "Octopus.Tentacle.CurrentDeployment.TargetedRoles";
            }

            public static class PreviousInstallation
            {
                [Define(Description = "The previous version of the package that was deployed to the Tentacle", Example = "1.2.3")] public static readonly string PackageVersion = "Octopus.Tentacle.PreviousInstallation.PackageVersion";
                [Define(Description = "The path to the package file previously deployed", Example = "C:\\Octopus\\Tentacle\\Packages\\OctoFx.1.2.2.nupkg")] public static readonly string PackageFilePath = "Octopus.Tentacle.PreviousInstallation.PackageFilePath";
                [Define(Description = "The directory into which the previous version of the package was extracted", Example = "C:\\Octopus\\Tentacle\\Apps\\Production\\OctoFx\\1.2.2")] public static readonly string OriginalInstalledPath = "Octopus.Tentacle.PreviousInstallation.OriginalInstalledPath";
                [Define(Description = "The directory into which the previous version of the package was deployed", Example = "C:\\InetPub\\WWWRoot\\OctoFx")] public static readonly string CustomInstallationDirectory = "Octopus.Tentacle.PreviousInstallation.CustomInstallationDirectory";
            }

            public static class Agent
            {
                [Define(Category = VariableCategory.Agent, Description = "The directory under which the agent installs packages", Example = "C:\\Octopus\\Tentacle\\Apps")] public static readonly string ApplicationDirectoryPath = "Octopus.Tentacle.Agent.ApplicationDirectoryPath";
                [Define(Category = VariableCategory.Agent, Description = "The instance name that the agent runs under", Example = "Tentacle")] public static readonly string InstanceName = "Octopus.Tentacle.Agent.InstanceName";
                [Define(Category = VariableCategory.Agent, Description = "The directory containing the agent's own executables", Example = "C:\\Program Files\\Octopus Deploy\\Tentacle")] public static readonly string ProgramDirectoryPath = "Octopus.Tentacle.Agent.ProgramDirectoryPath";
            }
        }

        public class Script
        {
            // Variables will have the form Octopus.Script.Module[ModuleName]
            public static readonly string ModulePrefix = "Octopus.Script.Module";
        }

        public static class Action
        {
            [Define(Category = VariableCategory.Action, Example = "85287bef-fe6c-4eb7-beef-74f5e5a6b5b0")] public static readonly string Id = "Octopus.Action.Id";
            [Define(Category = VariableCategory.Action, Example = "Website")] public static readonly string Name = "Octopus.Action.Name";
            [Define(Category = VariableCategory.Action, Description = "The sequence number of the action in the deployment process", Example = "5", Domain = VariableDomain.Number)] public static readonly string Number = "Octopus.Action.Number";
            [Define(Category = VariableCategory.Hidden)] public static readonly string IsTentacleDeployment = "Octopus.Action.IsTentacleDeployment";
            [Define(Category = VariableCategory.Hidden)] public static readonly string IsFtpDeployment = "Octopus.Action.IsFtpDeployment";
            [Define(Category = VariableCategory.Action, Description = "Machine roles targeted by the action", Example = "web-server,frontend", Domain = VariableDomain.List)] public static readonly string TargetRoles = "Octopus.Action.TargetRoles";
            [Define(Category = VariableCategory.Action, Description = "The maximum number of machines on which the action will concurrently execute", Example = "5", Domain = VariableDomain.Number)] public static readonly string MaxParallelism = "Octopus.Action.MaxParallelism";
            [Define(Category = VariableCategory.Action, Description = "Whether or not the action has been skipped in the current deployment", Example = "True", Domain = VariableDomain.Boolean)] public static readonly string IsSkipped = "Octopus.Action.IsSkipped";
            [Define(Category = VariableCategory.Action, Description = "If set by the user, completes processing of the action without runnning further conventions/scripts", Example = "True", Domain = VariableDomain.Boolean)] public static readonly string SkipRemainingConventions = "Octopus.Action.SkipRemainingConventions";
            [Define(Category = VariableCategory.Hidden)] public static readonly string EnabledFeatures = "Octopus.Action.EnabledFeatures";

            public static class Output
            {
                [Define(Category = VariableCategory.Output, Pattern = "Octopus.Action[_name_].Output._property_", Description = "The results of calling `Set-OctopusVariable` during an action are exposed for use in other actions using this pattern", Example = "Octopus.Action[Website].Output.WarmUpResponseTime")] public static readonly string Prefix = "Octopus.Action.Output";
            }

            public static class Template
            {
                [Define(Category = VariableCategory.Action, Description = "If the action is based on a step template, the ID of the template", Example = "actiontemplates-123")] public static readonly string Id = "Octopus.Action.Template.Id";
                [Define(Category = VariableCategory.Action, Description = "If the action is based on a step template, the version of the template in use", Example = "123", Domain = VariableDomain.Number)] public static readonly string Version = "Octopus.Action.Template.Version";
            }

            public static class Package
            {
                public static readonly string ActionTypeName = "Octopus.TentaclePackage";
                [Define(Category = VariableCategory.Action, Description = "The ID of the NuGet package being deployed", Example = "OctoFx.RateService")] public static readonly string NuGetPackageId = "Octopus.Action.Package.NuGetPackageId";
                [Define(Category = VariableCategory.Action, Description = "The version of the NuGet package being deployed", Example = "1.2.3")] public static readonly string NuGetPackageVersion = "Octopus.Action.Package.NuGetPackageVersion";
                [Define(Category = VariableCategory.Action, Description = "If true, the package will be downloaded by the Tentacle, rather than pushed by the Octopus server", Example = "False", Domain = VariableDomain.Boolean)] public static readonly string ShouldDownloadOnTentacle = "Octopus.Action.Package.DownloadOnTentacle";
                [Define(Category = VariableCategory.Action, Description = "The ID of the NuGet feed from which the package being deployed was pulled", Example = "feeds-123")] public static readonly string NuGetFeedId = "Octopus.Action.Package.NuGetFeedId";
                [Define(Category = VariableCategory.Hidden)] public static readonly string UpdateIisWebsite = "Octopus.Action.Package.UpdateIisWebsite";
                [Define(Category = VariableCategory.Hidden)] public static readonly string UpdateIisWebsiteName = "Octopus.Action.Package.UpdateIisWebsiteName";
                [Define(Category = VariableCategory.Action, Description = "If set, a specific directory to which the package will be copied after extraction", Example = "C:\\InetPub\\WWWRoot\\OctoFx")] public static readonly string CustomInstallationDirectory = "Octopus.Action.Package.CustomInstallationDirectory";

                [Define(Category = VariableCategory.Action, Description = "If true, the all files in the `Octopus.Action.Package.CustomInstallationDirectory` will be deleted before deployment", Example = "False", Domain = VariableDomain.Boolean)] public static readonly string CustomInstallationDirectoryShouldBePurgedBeforeDeployment =
                    "Octopus.Action.Package.CustomInstallationDirectoryShouldBePurgedBeforeDeployment";

                [Define(Category = VariableCategory.Hidden)] public static readonly string AutomaticallyUpdateAppSettingsAndConnectionStrings = "Octopus.Action.Package.AutomaticallyUpdateAppSettingsAndConnectionStrings";
                [Define(Category = VariableCategory.Hidden)] public static readonly string AutomaticallyRunConfigurationTransformationFiles = "Octopus.Action.Package.AutomaticallyRunConfigurationTransformationFiles";
                [Define(Category = VariableCategory.Hidden)] [DeprecatedAlias("Octopus.Action.Package.IgnoreConfigTranformationErrors")] public static readonly string IgnoreConfigTransformationErrors = "Octopus.Action.Package.IgnoreConfigTransformationErrors";
                [Define(Category = VariableCategory.Hidden)] public static readonly string SuppressConfigTransformationLogging = "Octopus.Action.Package.SuppressConfigTransformationLogging";
                [Define(Category = VariableCategory.Hidden)] public static readonly string AdditionalXmlConfigurationTransforms = "Octopus.Action.Package.AdditionalXmlConfigurationTransforms";
                [Define(Category = VariableCategory.Action, Description = "If true, and the version of the package being deployed is already present on the machine, its re-deployment will be skipped (use with caution)", Example = "False", Domain = VariableDomain.Boolean)] public static readonly string SkipIfAlreadyInstalled = "Octopus.Action.Package.SkipIfAlreadyInstalled";
                [Define(Category = VariableCategory.Hidden)] public static readonly string IgnoreVariableReplacementErrors = "Octopus.Action.Package.IgnoreVariableReplacementErrors";

                public class Output
                {
                    [Define(
                        Category = VariableCategory.Output,
                        Pattern = "Octopus.Action[_name_].Output.Package.InstallationDirectoryPath",
                        Example = "C:\\Octopus\\Tentacle\\Apps\\Production\\MyApp\\1.2.3",
                        Description = "The directory to which the package was installed")] public static readonly string InstallationDirectoryPath = "Package.InstallationDirectoryPath";
                }

                public static class Ssh
                {
                    [Define(Category = VariableCategory.Action, Description = "The root directory used for deployment on the target machine", Example = "/home/user/.tentacle/")] public static readonly string RootDirectoryPath = "Octopus.Action.Package.Ssh.RootDirectoryPath";
                    [Define(Category = VariableCategory.Action, Description = "The tools directory used for deployment on the target machine", Example = "/home/user/.tentacle/tools/")] public static readonly string ToolsDirectoryPath = "Octopus.Action.Package.Ssh.ToolsDirectoryPath";
                    [Define(Category = VariableCategory.Action, Description = "The applications directory used for deployment on the target machine", Example = "/home/user/.tentacle/apps/")] public static readonly string ApplicationsDirectoryPath = "Octopus.Action.Package.Ssh.ApplicationsDirectoryPath";
                    [Define(Category = VariableCategory.Action, Description = "The packages directory used for deployment on the target machine", Example = "/home/user/.tentacle/packages/")] public static readonly string PackagesDirectoryPath = "Octopus.Action.Package.Ssh.PackagesDirectoryPath";
                    [Define(Category = VariableCategory.Action, Description = "The directory used for all of the Tentacle's temporary files", Example = "/var/tmp/tentacle/SQ-ABC123/85287bef-fe6c-4eb7-beef-74f5e5a6b5b0")] public static readonly string TemporaryFilesDirectoryPath = "Octopus.Action.Package.Ssh.TempDirectoryPath";
                    [Define(Category = VariableCategory.Action, Description = "The package file being deployed on the target machine", Example = "/home/user/.tentacle/packages/OctoFx.RateService.1.2.3.nupkg.tar.gz")] public static readonly string PackageFileName = "Octopus.Action.Package.Ssh.PackageFileName";
                }
            }

            public static class Ftp
            {
                // deprecated step, do not use
                public static readonly string ActionTypeName = "Octopus.Ftp";
                [Define(Category = VariableCategory.Hidden)] public static readonly string Host = "Octopus.Action.Ftp.Host";
                [Define(Category = VariableCategory.Hidden)] public static readonly string Username = "Octopus.Action.Ftp.Username";
                [Define(Category = VariableCategory.Hidden)] public static readonly string Password = "Octopus.Action.Ftp.Password";
                [Define(Category = VariableCategory.Hidden)] public static readonly string UseFtps = "Octopus.Action.Ftp.UseFtps";
                [Define(Category = VariableCategory.Hidden)] public static readonly string FtpPort = "Octopus.Action.Ftp.FtpPort";
                [Define(Category = VariableCategory.Hidden)] public static readonly string RootDirectory = "Octopus.Action.Ftp.RootDirectory";
                [Define(Category = VariableCategory.Hidden)] public static readonly string DeleteDestinationFiles = "Octopus.Action.Ftp.DeleteDestinationFiles";
                [Define(Category = VariableCategory.Hidden)] public static readonly string UseActiveMode = "Octopus.Action.Ftp.UseActiveMode";
                [Define(Category = VariableCategory.Hidden)] public static readonly string SocketTimeoutMinutes = "Octopus.Action.Ftp.SocketTimeoutMinutes";
            }

            public static class Email
            {
                public static readonly string ActionTypeName = "Octopus.Email";
                [Define(Category = VariableCategory.Hidden)] public static readonly string To = "Octopus.Action.Email.To";
                [Define(Category = VariableCategory.Hidden)] public static readonly string CC = "Octopus.Action.Email.CC";
                [Define(Category = VariableCategory.Hidden)] public static readonly string Bcc = "Octopus.Action.Email.Bcc";
                [Define(Category = VariableCategory.Hidden)] public static readonly string IsHtml = "Octopus.Action.Email.IsHtml";
                [Define(Category = VariableCategory.Hidden)] public static readonly string Subject = "Octopus.Action.Email.Subject";
                [Define(Category = VariableCategory.Hidden)] public static readonly string Body = "Octopus.Action.Email.Body";
            }

            public static class CustomScripts
            {
                public static readonly string Prefix = "Octopus.Action.CustomScripts.";
            }

            public static class Script
            {
                public static readonly string ActionTypeName = "Octopus.Script";
                [Define(Category = VariableCategory.Action, Description = "The syntax of the script being run in a script step", Example = "PowerShell")] public static readonly string Syntax = "Octopus.Action.Script.Syntax";
                [Define(Category = VariableCategory.Action, Description = "The script being run in a script step", Example = "Write-Host 'Hello!'")] public static readonly string ScriptBody = "Octopus.Action.Script.ScriptBody";
            }

            public static class Manual
            {
                public static readonly string ActionTypeName = "Octopus.Manual";
                [Define(Category = VariableCategory.Action, Description = "The instructions provided for a manual step", Example = "Don't break anything :)")]
                public static readonly string Instructions = "Octopus.Action.Manual.Instructions";
                [Define(Category = VariableCategory.Action, Description = "The teams responsible for completing a manual step", Example = "teams-123,teams-124", Domain = VariableDomain.List)] public static readonly string ResponsibleTeamIds = "Octopus.Action.Manual.ResponsibleTeamIds";

                public static class Output
                {
                    [Define(
                        Category = VariableCategory.Output,
                        Pattern = "Octopus.Action[_name_].Output.Manual.Notes",
                        Example = "Signed off by Alice",
                        Description = "Notes provided by the user who completed a manual step")] public static string Notes = "Manual.Notes";

                    public static class ResponsibleUser
                    {
                        [Define(Category = VariableCategory.Output,
                            Pattern = "Octopus.Action[_name_].Output.ResponsibleUser.Id",
                            Description = "The ID of the user who completed the manual step",
                            Example = "users-123")] public static readonly string Id = "Manual.ResponsibleUser.Id";

                        [Define(Category = VariableCategory.Output,
                            Pattern = "Octopus.Action[_name_].Output.ResponsibleUser.Username",
                            Description = "The username of the user who completed the manual step",
                            Example = "alice")] public static readonly string Username = "Manual.ResponsibleUser.Username";

                        [Define(Category = VariableCategory.Output,
                            Pattern = "Octopus.Action[_name_].Output.ResponsibleUser.DisplayName",
                            Description = "The full name of the user who completed the manual step",
                            Example = "Alice King")] public static readonly string DisplayName = "Manual.ResponsibleUser.DisplayName";

                        [Define(Category = VariableCategory.Output,
                            Pattern = "Octopus.Action[_name_].Output.ResponsibleUser.EmailAddress",
                            Description = "The email address of the user who completed the manual step",
                            Example = "alice@example.com")] public static readonly string EmailAddress = "Manual.ResponsibleUser.EmailAddress";
                    }
                }
            }

            public static class Azure
            {
                // do not reuse this value
                [Define(Category = VariableCategory.Hidden)]
                public static readonly string ActionTypeNameForDeprecatedAzureStep = "Octopus.Azure";

                [Define(Category = VariableCategory.Hidden)]
                public static readonly string CloudServiceActionTypeName = "Octopus.AzureCloudService";

                [Define(Category = VariableCategory.Hidden)]
                public static readonly string WebAppActionTypeName = "Octopus.AzureWebApp";

                [Define(Category = VariableCategory.Hidden)]
                public static readonly string PowerShellActionTypeName = "Octopus.AzurePowerShell";

                [Define(Category = VariableCategory.Action, Description = "The ID of the Octopus Azure subscription account", Example = "accounts-1")]
                public static readonly string AccountId = "Octopus.Action.Azure.AccountId";

                [Define(Category = VariableCategory.Hidden)]
                public static readonly string PowershellModulePath = "Octopus.Action.Azure.PowerShellModule";

                [Define(Category = VariableCategory.Hidden)] public static readonly string PackageExtractionPath = "Octopus.Action.Azure.PackageExtractionPath";
                [Define(Category = VariableCategory.Action, Description = "Azure account subscription ID", Example = "42d91e16-206f-4a14-abd2-24791cbbc522")] public static readonly string SubscriptionId = "Octopus.Action.Azure.SubscriptionId";
                [Define(Category = VariableCategory.Action, Description = "Thumprint of the certificate used for Azure authentication ", Example = "0320C45A19F9FAE120356EAB95AB2377C19FF3E2")] public static readonly string CertificateThumbprint = "Octopus.Action.Azure.CertificateThumbprint";
                [Define(Category = VariableCategory.Action, Description = "Base64-encoded certificate used for Azure authentication", Example = "no example provided")] public static readonly string CertificateBytes = "Octopus.Action.Azure.CertificateBytes";

                [Define(Category = VariableCategory.Action, Description = "Azure WebApp name", Example = "AcmeOnline")] public static readonly string WebAppName = "Octopus.Action.Azure.WebAppName";
                [Define(Category = VariableCategory.Action, Description = "Remove additional files on the destination that are not part of the deployment", Example = "False")] public static readonly string RemoveAdditionalFiles = "Octopus.Action.Azure.RemoveAdditionalFiles";
                [Define(Category = VariableCategory.Action, Description = "Do not remove the contents of the App_Data folder", Example = "False")] public static readonly string PreserveAppData = "Octopus.Action.Azure.PreserveAppData";
                [Define(Category = VariableCategory.Action, Description = "Physical path relative to site root", Example = "one\\two")] public static readonly string PhysicalPath = "Octopus.Action.Azure.PhysicalPath";

                [Define(Category = VariableCategory.Action, Description = "Azure Cloud Service name", Example = "AcmeService")] public static readonly string CloudServiceName = "Octopus.Action.Azure.CloudServiceName";
                [Define(Category = VariableCategory.Action, Description = "Azure Storage Account name", Example = "AcmePackageStore")] public static readonly string StorageAccountName = "Octopus.Action.Azure.StorageAccountName";
                [Define(Category = VariableCategory.Action, Description = "Azure deployment slot", Example = "Production")] public static readonly string Slot = "Octopus.Action.Azure.Slot";
                [Define(Category = VariableCategory.Action, Description = "Swap current Staging deployment if possible", Example = "False")] public static readonly string SwapIfPossible = "Octopus.Action.Azure.SwapIfPossible";
                [Define(Category = VariableCategory.Action, Description = "Replace instance counts in configuration file with those currently configured in Azure portal", Example = "False")] public static readonly string UseCurrentInstanceCount = "Octopus.Action.Azure.UseCurrentInstanceCount";
                [Define(Category = VariableCategory.Action, Description = "Log extracted Cloud Service Package", Example="False")] public static readonly string LogExtractedCspkg = "Octopus.Action.Azure.LogExtractedCspkg";
                [Define(Category = VariableCategory.Action, Description = "Relative path to the *.cscfg file", Example = "AcmeService-Production.cscfg")][DeprecatedAlias("OctopusAzureConfigurationFileName")] public static readonly string CloudServiceConfigurationFileRelativePath = "Octopus.Action.Azure.CloudServiceConfigurationFileRelativePath";
            }
        }
    }
}