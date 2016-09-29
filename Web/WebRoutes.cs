using System.Security.Cryptography;
using System.Text;

namespace Octopus.Shared.Web
{
    public static class WebRoutes
    {
        // Url templates: http://tools.ietf.org/html/rfc6570
        public static class Api
        {
            public static class Home
            {
                public static string Index = "~/api";
            }

            public static class Accounts
            {
                public static string Template = "~/api/accounts{/id}{?skip}";
                public static string Index = "~/api/accounts{?skip}";
                public static string Get = "~/api/accounts/{id}";
                public static string PublicKey = "~/api/accounts/{id}/pk";

                public static class Azure
                {
                    public static string CloudServices = "~/api/accounts/{id}/cloudServices";
                    public static string StorageAccounts = "~/api/accounts/{id}/storageAccounts";
                    public static string WebSites = "~/api/accounts/{accountId}/websites";
                    public static string ResourceGroups = "~/api/accounts/{id}/resourceGroups";
                }
            }

            public static class ActionTemplates
            {
                public static string Template = "~/api/actiontemplates{/id}{?skip}";
                public static string Index = "~/api/actiontemplates{?skip}";
                public static string Get = "~/api/actiontemplates/{id}";
                public static string Usage = "~/api/actiontemplates/{id}/usage";
            }

            public static class Alerts
            {
                public static string Template = "~/api/alerts{/id}{?skip}";
                public static string Index = "~/api/alerts";
            }

            public static class Artifacts
            {
                public static string Template = "~/api/artifacts{/id}{?skip,regarding}";
                public static string Index = "~/api/artifacts{?skip,regarding}";
                public static string Get = "~/api/artifacts/{id}";
                public static string GetContent = "~/api/artifacts/{id}/content{/filename}";
            }

            public static class SmtpConfiguration
            {
                public static string Template = "~/api/smtpconfiguration";
            }

            public static class BuiltInRepositoryConfiguration
            {
                public static string Template = "~/api/repository/configuration";
            }

            public static class Environments
            {
                public static string Template = "~/api/environments{/id}{?skip,ids}";
                public static string Index = "~/api/environments{?skip,ids}";
                public static string Get = "~/api/environments/{id}";
                public static string GetMachines = "~/api/environments/{id}/machines{?skip}";
                public static string SortOrder = "~/api/environments/sortorder";
            }

            public static class Machines
            {
                public static string Template = "~/api/machines{/id}{?skip,thumbprint,deploymentId}";
                public static string Index = "~/api/machines{?skip}";
                public static string Get = "~/api/machines/{id}";
                public static string GetConnection = "~/api/machines/{id}/connection";
                public static string GetTasks = "~/api/machines/{id}/tasks{?skip}";
                public static string Discover = "~/api/machines/discover{?host,port,type}";
            }

            public static class MachinesRoles
            {
                public static string Index = "~/api/machineroles/all";
            }

            public static class MaintenanceConfiguration
            {
                public static string Template = "~/api/maintenanceconfiguration";
            }

            public static class FeaturesConfiguration
            {
                public static string Template = "~/api/featuresconfiguration";
            }

            public static class ServerConfiguration
            {
                public static string Template = "~/api/serverconfiguration";
            }

            public static class ServerConfigurationSettings
            {
                public static string Template = "~/api/serverconfigurationsettings";
            }

            public static class Teams
            {
                public static string Template = "~/api/teams{/id}{?skip}";
                public static string Index = "~/api/teams{?skip}";
                public static string Get = "~/api/teams/{id}";
            }

            public static class Proxies
            {
                public static string Template = "~/api/proxies{/id}{?skip}";
                public static string Index = "~/api/proxies{?skip}";
                public static string Get = "~/api/proxies/{id}";
            }

            public static class PermissionDescriptions
            {
                public static string Template = "~/api/permissions/all";
            }

            public static class ExternalSecurityGroupProviders
            {
                public static string Index = "~/api/externalsecuritygroupproviders";
            }

            public static class UserRoles
            {
                public static string Template = "~/api/userroles{/id}{?skip}";
                public static string Index = "~/api/userroles{?skip}";
                public static string Get = "~/api/userroles/{id}";
            }

            public static class Releases
            {
                public static string Template = "~/api/releases{/id}{?skip,ignoreChannelRules}";
                public static string Index = "~/api/releases{?skip}";
                public static string Get = "~/api/releases/{id}";
                public static string Progression = "~/api/releases/{id}/progression";
                public static string GetDeployments = "~/api/releases/{id}/deployments{?skip}";
                public static string DeploymentTemplate = "~/api/releases/{id}/deployments/template";
                public static string DeploymentPreview = "~/api/releases/{id}/deployments/preview/{environment}";
                public static string DeploymentTenantPreview = "~/api/releases/{id}/deployments/preview/{environment}/{tenant}";
                public static string SnapshotVariables = "~/api/releases/{id}/snapshot-variables";
            }

            public static class Deployments
            {
                public static string Template = "~/api/deployments{/id}{?skip,take,projects,environments,channels,taskState}";
                public static string Index = "~/api/deployments{?skip,take,projects,environments,channels,taskState}";
                public static string Get = "~/api/deployments/{id}";
            }

            public static class Defect
            {
                //public static string Template = "~/api/releases/{id}/defects{?skip,take}";

                public static string Index = "~/api/releases/{id}/defects";
                public static string Report = "~/api/releases/{id}/defects";
                public static string Resolve = "~/api/releases/{id}/defects/resolve";
            }

            public static class RetentionPolicies
            {
                public static string Template = "~/api/retentionpolicies{/id}{?skip}";
                public static string Index = "~/api/retentionpolicies{?skip}";
                public static string Get = "~/api/retentionpolicies/{id}";
            }

            public static class Lifecycles
            {
                public static string Template = "~/api/lifecycles{/id}{?skip}";
                public static string Index = "~/api/lifecycles{?skip}";
                public static string Get = "~/api/lifecycles/{id}";
                public static string Preview = "~/api/lifecycles/{id}/preview";
                public static string Projects = "~/api/lifecycles/{id}/projects";
            }

            public static class MachinePolicies
            {
                public static string Template = "~/api/machinepolicies{/id}{?skip}";
                public static string Index = "~/api/machinepolicies{?skip}";
                public static string Get = "~/api/machinepolicies/{id}";
                public static string Machines = "~/api/machinepolicies/{id}/machines{?skip}";
            }

            public static class Subscriptions
            {
                public static string Template = "~/api/subscriptions{/id}{?skip}";
                public static string Index = "~/api/subscriptions{?skip}";
                public static string Get = "~/api/subscriptions/{id}";
            }

            public static class ProjectGroups
            {
                public static string Template = "~/api/projectgroups{/id}{?skip}";
                public static string Index = "~/api/projectgroups{?skip}";
                public static string Get = "~/api/projectgroups/{id}";
                // ReSharper disable once MemberHidesStaticFromOuterClass
                public static string Projects = "~/api/projectgroups/{id}/projects{?skip}";
            }

            public static class Projects
            {
                public static string Template = "~/api/projects{/id}{?skip,ids,clone}";
                public static string Index = "~/api/projects{?skip}";
                public static string Get = "~/api/projects/{id}";
                public static string Logo = "~/api/projects/{id}/logo";
                public static string GetReleases = "~/api/projects/{id}/releases{/version}{?skip}";
                public static string GetChannels = "~/api/projects/{id}/channels";
                public static string GetTriggers = "~/api/projects/{id}/triggers";
                public static string OrderChannels = "~/api/projects/{id}/channels/order";
                public static string Pulse = "~/api/projects/pulse{?projectIds}";
            }

            public static class ProjectTriggers
            {
                public static string Template = "~/api/projecttriggers{/id}";
                public static string Get = "~/api/projecttriggers/{id}";
            }

            public static class Tasks
            {
                public static string Template = "~/api/tasks{/id}{?skip,active,environment,project,name,node,running}";
                public static string Index = "~/api/tasks{?skip,active,environment,project,name,node,running}";
                public static string Get = "~/api/tasks/{id}";
                public static string Details = "~/api/tasks/{id}/details{?verbose,tail}";
                public static string Raw = "~/api/tasks/{id}/raw";
                public static string QueuedBehind = "~/api/tasks/{id}/queued-behind{?skip}";
                public static string Rerun = "~/api/tasks/rerun/{id}";
                public static string Cancel = "~/api/tasks/{id}/cancel";
            }

            public static class Scheduler
            {
                public static string Get = "~/api/scheduler/{name}/logs{?verbose,tail}";
            }

            public static class Events
            {
                public static string Template = "~/api/events{/id}{?skip,regarding,regardingAny,users,projects,environments,eventGroups,eventCategories,tags,tenants,from,to,internal}";
                public static string Index = "~/api/events{?skip,regarding,regardingAny,users,projects,environments,eventGroups,eventCategories,tags,tenants,from,to,internal}";
                public static string Get = "~/api/events/{id}";
                public static string GetCategories = "~/api/events/categories";
                public static string GetGroups = "~/api/events/groups";
            }

            public static class Feeds
            {
                public static string Template = "~/api/feeds{/id}{?skip}";
                public static string Index = "~/api/feeds{?skip}";
                public static string Get = "~/api/feeds/{id}";
            }

            public static class Interruptions
            {
                public static string Template = "~/api/interruptions{/id}{?skip,regarding,pendingOnly}";
                public static string Index = "~/api/interruptions{?skip,regarding,pendingOnly}";
                public static string Get = "~/api/interruptions/{id}";
                public static string Submit = "~/api/interruptions/{id}/submit";
                public static string Responsible = "~/api/interruptions/{id}/responsible";
            }

            public static class Variables
            {
                public static string Template = "~/api/variables{/id}";
                public static string Index = "~/api/Variables{?skip,ids}";
                public static string Get = "~/api/variables/{id}";
                public static string ScopeValues = "~/api/variables/scope-values/{ownerId}";
                public static string Names = "~/api/variables/names{?project,projectEnvironmentsFilter}";
                public static string NonPrintableChars = "~/api/variables/nonPrintableChars";
            }

            public static class Progression
            {
                public static string Get = "~/api/progression/{id}";
            }

            public static class DeploymentProcesses
            {
                public static string Template = "~/api/deploymentprocesses{/id}";
                public static string Get = "~/api/deploymentprocesses/{id}";
                public static string GetTemplate = "~/api/deploymentprocesses/{id}/template{?channel,releaseId}";
            }

            public static class Dashboards
            {
                public static string Template = "~/api/dashboard{?projectId,releaseId,selectedTenants,selectedTags,showAll}";
                public static string DynamicTemplate = "~/api/dashboard/dynamic{?projects,environments,includePrevious}";
            }

            public static class DashboardConfigurations
            {
                public static string Template = "~/api/dashboardconfiguration";
            }

            public static class Licenses
            {
                public static string Current = "~/api/licenses/licenses-current";
            }

            public static class Users
            {
                public static string Index = "~/api/users{?skip}";
                public static string Template = "~/api/users{/id}{?skip}";
                public static string Get = "~/api/users/{id}";
                public static string Logout = "~/api/users/logout";
                public static string Register = "~/api/users/register";
                public static string Me = "~/api/users/me";
                public static string Permissions = "~/api/users/{id}/permissions";
                public static string Invitations = "~/api/users/invitations";
                public static string GetInvitation = "~/api/users/invitations/{id}";
            }

            public static class ApiKeys
            {
                public static string Template = "~/api/users/{userId}/apikeys{/id}{?skip}";
            }

            public static class Reporting
            {
                public static string DeploymentsCountedByWeekTemplate = "~/api/reporting/deployments-counted-by-week{?projectIds}";
            }

            public static class LibraryVariableSets
            {
                public static string Template = "~/api/libraryvariablesets{/id}{?skip,contentType,ids}";
                public static string Index = "~/api/libraryvariablesets{?skip,contentType,ids}";
                public static string Get = "~/api/libraryvariablesets/{id}";
            }

            public static class ServerStatus
            {
                public static string Index = "~/api/serverstatus";
                public static string RecentLogs = "~/api/serverstatus/logs";
                public static string Activities = "~/api/serverstatus/activities";
                public static string RawActivities = "~/api/serverstatus/activities/raw";
                public static string RawActivity = "~/api/serverstatus/activities/raw/{id}";
                public static string SystemInfo = "~/api/serverstatus/system-info";
                public static string SystemReport = "~/api/serverstatus/system-report";
                public static string BuiltInFeedStats = "~/api/serverstatus/nuget";
                public static string ExtensionStats = "~/api/serverstatus/extensions";
                public static string GCCollect = "~/api/serverstatus/gc-collect";
            }

            public static class BuiltInFeed
            {
                public static string Push = "~/nuget/packages";
            }

            public static class Certificates
            {
                public static string Template = "~/api/certificates{/id}";
                public static string Index = "~/api/certificates";
                public static string Get = "~/api/certificates/{id}";
            }

            public static class CertificateConfiguration
            {
                public static string Template = "~/api/configuration/certificates{/id}{?skip}";
                public static string Index = "~/api/configuration/certificates{?skip}";
                public static string Get = "~/api/configuration/certificates/{id}";
                public static string PublicCer = "~/api/configuration/certificates/{id}/public-cer";
            }

            public static class Packages
            {
                public static string Search = "~/api/feeds/{id}/packages{?packageId,partialMatch,includeMultipleVersions,includeNotes,includePreRelease,versionRange,preReleaseTag,take}";
                public static string Versions = "~/api/feeds/{id}/packages{?packageIds}";
                public static string Notes = "~/api/feeds/{id}/packages/notes{?packageId,version}";
                public static string Template = "~/api/packages{/id}{?nuGetPackageId,filter,latest,skip,take,includeNotes}";
                public static string Get = "~/api/packages/{id}{?includeNotes}";
                public static string Raw = "~/api/packages/{id}/raw";
                public static string Upload = "~/api/packages/raw{?replace}";
                public static string Bulk = "~/api/packages/bulk{?ids}";
            }

            public static class OctopusServerNodes
            {
                public static string Template = "~/api/octopusservernodes{/id}";
                public static string Index = "~/api/octopusservernodes";
                public static string Get = "~/api/octopusservernodes/{id}";
            }

            public static class Channels
            {
                public static string Template = "~/api/channels{/id}";
                public static string Get = "~/api/channels/{id}";
                public static string VersionRuleTest = "~/api/channels/rule-test{?version,versionRange,preReleaseTag}";
                public static string GetReleases = "~/api/channels/{id}/releases{?skip}";
            }

            public static class Tenants
            {
                public static string Template = "~/api/tenants{/id}{?skip,projectId,name,tags}";
                public static string Get = "~/api/tenants/{id}";
                public static string All = "~/api/tenants/all{?projectId,ids,name,tags}";
                public static string Index = "~/api/tenants{?skip,projectId,ids}";
                public static string Logo = "~/api/tenants/{id}/logo";
                public static string Variables = "~/api/tenants/{id}/variables";
                public static string TenantTagTest = "~/api/tenants/tag-test{?tenantIds,tags}";
                public static string MissingVariables = "~/api/tenants/variables-missing{?tenantId,projectId,environmentId,includeDetails}";
            }

            public static class TenantVariables
            {
                public static string Template = "~/api/tenantvariables/all{?projectId}";
            }

            public static class TagSets
            {
                public static string Template = "~/api/tagsets{/id}";
                public static string Get = "~/api/tagsets/{id}";
                public static string Index = "~/api/tagsets{?skip}";
                public static string SortOrder = "~/api/tagsets/sortorder";
            }
        }

        public static class Web
        {
            public static class Tasks
            {
                public static string Show = "~/app#/tasks/{id}";
            }

            public static class Projects
            {
                public static string Show = "~/app#/projects/{id}";
            }

            public static class Deployments
            {
                public static string Show = "~/app#/deployments/{id}";
            }

            public static class Releases
            {
                public static string Show = "~/app#/releases/{id}";
            }

            public class Dashboards
            {
                public static string Index = "~/app";
            }

            public static class Users
            {
                public static string Register = "~/app#/users/register/{invitation}";
            }
        }

        public static class External
        {
            public static class Users
            {
                // Because the hash is algorithmic, not a straight substitution, it makes sense to
                // compute it here.
                public static string Gravatar(string email)
                {
                    string hexMd5;
                    var gravatarHash = TryHexMd5(email.Trim().ToLowerInvariant(), out hexMd5) ? hexMd5 : "00000000000000000000000000000000";
                    return "https://www.gravatar.com/avatar/" + gravatarHash + "?d=blank";
                }

                static bool TryHexMd5(string s, out string hexMd5)
                {
                    hexMd5 = null;
                    try
                    {
                        var md5 = MD5.Create();
                        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(s));
                        var result = new StringBuilder();
                        foreach (var b in hash)
                        {
                            result.Append(b.ToString("x2"));
                        }
                        hexMd5 = result.ToString();
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
        }
    }
}
