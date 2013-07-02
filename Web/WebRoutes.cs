using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using Octopus.Client.Model;

namespace Octopus.Shared.Web
{
    public static class WebRoutes
    {
        public static partial class Api
        {
            public static class Home
            {
                public static string Index = "/api";
            }
            
            public static class Environments
            {
                public static string Template = "/api/environments{/id}{?nonStale,skip}";
                public static string Index = "/api/environments{?nonStale,skip}";
                public static string Get = "/api/environments/{id}";
                public static string GetMachines = "/api/environments/{id}/machines{?nonStale,skip}";
            }

            public static class Machines
            {
                public static string Template = "/api/machines{/id}{?nonStale,skip}";
                public static string Index = "/api/machines{?nonStale,skip}";
                public static string Get = "/api/machines/{id}";
            }

            public static class Tenants
            {
                public static string Template = "/api/tenants{/id}{?nonStale,skip}";
                public static string Index = "/api/tenants{?nonStale,skip}";
                public static string Get = "/api/tenants/{id}";
            }

            public static class Releases
            {
                public static string Template = "/api/releases{/id}{?nonStale,skip}";
                public static string Index = "/api/releases{?nonStale,skip}";
                public static string Get = "/api/releases/{id}";
                public static string GetDeployments = "/api/releases/{id}/deployments";
            }

            public static class Deployments
            {
                public static string Template = "/api/deployments{/id}{?nonStale,skip}";
                public static string Index = "/api/deployments{?nonStale,skip}";
                public static string Get = "/api/deployments/{id}";
            }

            public static class TenantPools
            {
                public static string Template = "/api/tenantpools{/id}{?nonStale,skip}";
                public static string Index = "/api/tenantpools{?nonStale,skip}";
                public static string Get = "/api/tenantpools/{id}";
                public static string GetTenants = "/api/tenantpools/{id}/tenants";
            }

            public static class ProjectGroups
            {
                public static string Template = "/api/projectgroups{/id}{?nonStale,skip}";
                public static string Index = "/api/projectgroups{?nonStale,skip}";
                public static string Get = "/api/projectgroups/{id}";
                public static string Projects = "/api/projectgroups/{id}/projects";
            }

            public static class Projects
            {
                public static string Template = "/api/projects{/id}{?nonStale,skip}";
                public static string Index = "/api/projects{?nonStale,skip}";
                public static string Get = "/api/projects/{id}";
                public static string GetReleases = "/api/projects/{id}/releases{?nonStale,skip}";
            }
            
            public static class Tasks
            {
                public static string Template = "/api/tasks{/id}{?nonStale,skip}";
                public static string Index = "/api/tasks{?nonStale,skip}";
                public static string Get = "/api/tasks/{id}";
                public static string Details = "/api/tasks/details/{id}";
                public static string Raw = "/api/tasks/raw/{id}";
                public static string Rerun = "/api/tasks/rerun/{id}";
                public static string Cancel = "/api/tasks/cancel/{id}";
            }

            public static class Events
            {
                public static string Template = "/api/events{/id}{?skip,regarding,user}";
                public static string Index = "/api/events{?skip,regarding,user}";
            }

            public static class Feeds
            {
                public static string Template = "/api/feeds{/id}{?nonStale,skip}";
                public static string Index = "/api/feeds{?nonStale,skip}";
                public static string Get = "/api/feeds/{id}";
            }

            public static class Variables
            {
                public static string Template = "/api/variables{/id}";
                public static string Get = "/api/variables/{id}";
            }

            public static class DeploymentProcesses
            {
                public static string Template = "/api/deploymentprocesses{/id}";
                public static string Get = "/api/deploymentprocesses/{id}";
            }

            public static class Users
            {
                public static string Index = "/api/users{?nonStale,skip}";
                public static string Get = "/api/users/{id}";
                public static string Login = "/api/users/login{?returnUrl}";
                public static string Logout = "/api/users/logout";
                public static string Register = "/api/users/register{?inviteCode}";
                public static string Me = "/api/users/me";
            }
        }

        public static partial class Web
        {
            public static class Accounts
            {
                public static string Login = "/accounts/login{?returnUrl}";
                public static string Logout = "/accounts/logout";
                public static string Register = "/accounts/register{?inviteCode}";
            }            
        }

        // Old format (generated), not converted yet
        static string Format(string format, IEnumerable<object> routeParameters, Dictionary<string, object> queryString)
        {
            var builder = new StringBuilder();
            builder.AppendFormat(format, routeParameters.Select(p => (object)HttpUtility.UrlEncode((p ?? string.Empty).ToString())).ToArray());

            if (queryString != null && queryString.Any(c => c.Value != null))
            {
                builder.Append("?");

                var first = true;
                foreach (var pair in queryString)
                {
                    if (pair.Value == null)
                        continue;

                    if (!first)
                    {
                        builder.Append("&");
                    }

                    first = false;
                    var value = (pair.Value).ToString();
                    value = HttpUtility.UrlEncode(value);

                    builder.Append(pair.Key).Append('=').Append(value);
                }
            }

            return builder.ToString();
        }
        public static partial class Api
        {
            public static class Groups
            {
                /// <summary>
                /// Returns a URI like: /api/groups?area=api&amp;groupname=Foo
                /// </summary>
                public static string Index(string groupName = "")
                {
                    return Format("/api/groups", new object[] { }, new Dictionary<string, object>() { { "groupName", groupName } });
                }

                /// <summary>
                /// Returns a URI like: /api/groups/Foo?area=api
                /// </summary>
                public static string Get(string id)
                {
                    return Format("/api/groups/{0}", new object[] { id }, new Dictionary<string, object>() { });
                }

            }
            public static class Packages
            {
                /// <summary>
                /// Returns a URI like: /api/feeds/Foo/packages?area=api&amp;packageid=Foo&amp;partialmatch=False&amp;includemultipleversions=False&amp;take=0
                /// </summary>
                public static string Index(string feedId, string packageId, bool partialMatch = false, bool includeMultipleVersions = true, int take = 20)
                {
                    return Format("/api/feeds/{0}/packages", new object[] { feedId }, new Dictionary<string, object>() { { "packageId", packageId }, { "partialMatch", partialMatch }, { "includeMultipleVersions", includeMultipleVersions }, { "take", take } });
                }

                /// <summary>
                /// Returns a URI like: /api/feeds/Foo/packages/versions?area=api&amp;packageids=System.String%5B%5D
                /// </summary>
                public static string Versions(string feedId, System.String[] packageIds)
                {
                    return Format("/api/feeds/{0}/packages/versions", new object[] { feedId }, new Dictionary<string, object>() { { "packageIds", packageIds } });
                }

                /// <summary>
                /// Returns a URI like: /api/feeds/Foo/packages/notes?area=api&amp;packageid=Foo&amp;version=Foo
                /// </summary>
                public static string Notes(string feedId, string packageId, string version)
                {
                    return Format("/api/feeds/{0}/packages/notes", new object[] { feedId }, new Dictionary<string, object>() { { "packageId", packageId }, { "version", version } });
                }

            }
            public static class Preferences
            {
            }
            public static class Steps
            {
                /// <summary>
                /// Returns a URI like: /api/projects/Foo/steps?area=api
                /// </summary>
                public static string Index(string projectId)
                {
                    return Format("/api/projects/{0}/steps", new object[] { projectId }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /api/projects/Foo/steps/Foo?area=api
                /// </summary>
                public static string Get(string projectId, string id)
                {
                    return Format("/api/projects/{0}/steps/{1}", new object[] { projectId, id }, new Dictionary<string, object>() { });
                }

            }
        }
        public static class Configuration
        {
            public static class RetentionPolicies
            {
                /// <summary>
                /// Returns a URI like: /api/retentionpolicies?area=configuration
                /// </summary>
                public static string Index()
                {
                    return Format("/api/retentionpolicies", new object[] { }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /api/retentionpolicies/edit?area=configuration&amp;id=Foo
                /// </summary>
                public static string Edit(string id)
                {
                    return Format("/api/retentionpolicies/edit", new object[] { }, new Dictionary<string, object>() { { "id", id } });
                }

            }
            public static class Smtp
            {
                /// <summary>
                /// Returns a URI like: /api/smtp?area=configuration
                /// </summary>
                public static string Index()
                {
                    return Format("/api/smtp", new object[] { }, new Dictionary<string, object>() { });
                }

            }
            public static class Certificates
            {
                /// <summary>
                /// Returns a URI like: /api/certificates?area=configuration
                /// </summary>
                public static string Index()
                {
                    return Format("/api/certificates", new object[] { }, new Dictionary<string, object>() { });
                }

            }
            public static class Licenses
            {
                /// <summary>
                /// Returns a URI like: /api/licenses?area=configuration
                /// </summary>
                public static string Index()
                {
                    return Format("/api/licenses", new object[] { }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /api/licenses/edit?area=configuration
                /// </summary>
                public static string Edit()
                {
                    return Format("/api/licenses/edit", new object[] { }, new Dictionary<string, object>() { });
                }

            }
            public static class Permissions
            {
                /// <summary>
                /// Returns a URI like: /api/permissions?area=configuration
                /// </summary>
                public static string Index()
                {
                    return Format("/api/permissions", new object[] { }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /api/permissions/permissiondetails?area=configuration
                /// </summary>
                public static string PermissionDetails()
                {
                    return Format("/api/permissions/permissiondetails", new object[] { }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /api/permissions/edit?area=configuration&amp;id=Foo
                /// </summary>
                public static string Edit(string id)
                {
                    return Format("/api/permissions/edit", new object[] { }, new Dictionary<string, object>() { { "id", id } });
                }

                /// <summary>
                /// Returns a URI like: /api/permissions/test?area=configuration
                /// </summary>
                public static string Test()
                {
                    return Format("/api/permissions/test", new object[] { }, new Dictionary<string, object>() { });
                }

            }
            public static class Overview
            {
                /// <summary>
                /// Returns a URI like: /api/overview?area=configuration
                /// </summary>
                public static string Index()
                {
                    return Format("/api/overview", new object[] { }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /api/overview/checklist?area=configuration
                /// </summary>
                public static string Checklist()
                {
                    return Format("/api/overview/checklist", new object[] { }, new Dictionary<string, object>() { });
                }

            }
            public static class Groups
            {
                /// <summary>
                /// Returns a URI like: /api/groups?area=configuration&amp;skip=0
                /// </summary>
                public static string Index(int skip = 0)
                {
                    return Format("/api/groups", new object[] { }, new Dictionary<string, object>() { { "skip", skip } });
                }

                /// <summary>
                /// Returns a URI like: /api/groups/edit?area=configuration&amp;id=Foo
                /// </summary>
                public static string Edit(string id = null)
                {
                    return Format("/api/groups/edit", new object[] { }, new Dictionary<string, object>() { { "id", id } });
                }

            }
            public static class Storage
            {
                /// <summary>
                /// Returns a URI like: /api/storage?area=configuration
                /// </summary>
                public static string Index()
                {
                    return Format("/api/storage", new object[] { }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /api/storage/editbackup?area=configuration
                /// </summary>
                public static string EditBackup()
                {
                    return Format("/api/storage/editbackup", new object[] { }, new Dictionary<string, object>() { });
                }

            }
            public static class Feeds
            {
                /// <summary>
                /// Returns a URI like: /api/feeds?area=configuration
                /// </summary>
                public static string Index()
                {
                    return Format("/api/feeds", new object[] { }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /api/feeds/edit?area=configuration&amp;id=Foo
                /// </summary>
                public static string Edit(string id)
                {
                    return Format("/api/feeds/edit", new object[] { }, new Dictionary<string, object>() { { "id", id } });
                }

                /// <summary>
                /// Returns a URI like: /api/feeds/delete?area=configuration&amp;id=Foo
                /// </summary>
                public static string Delete(string id)
                {
                    return Format("/api/feeds/delete", new object[] { }, new Dictionary<string, object>() { { "id", id } });
                }

                /// <summary>
                /// Returns a URI like: /api/feeds/test?area=configuration&amp;id=Foo
                /// </summary>
                public static string Test(string id)
                {
                    return Format("/api/feeds/test", new object[] { }, new Dictionary<string, object>() { { "id", id } });
                }

            }
            public static class Users
            {
                /// <summary>
                /// Returns a URI like: /api/users?area=configuration&amp;skip=0
                /// </summary>
                public static string Index(int skip = 0)
                {
                    return Format("/api/users", new object[] { }, new Dictionary<string, object>() { { "skip", skip } });
                }

                /// <summary>
                /// Returns a URI like: /api/users/edit?area=configuration&amp;id=Foo
                /// </summary>
                public static string Edit(string id)
                {
                    return Format("/api/users/edit", new object[] { }, new Dictionary<string, object>() { { "id", id } });
                }

            }
        }
        public static partial class Web
        {
            public static class Events
            {
                /// <summary>
                /// Returns a URI like: /events/listevents?documentids=System.String%5B%5D&amp;scope=Application&amp;excludedeployments=False
                /// </summary>
                public static string ListEvents(System.String[] documentIds, EventScope scope = EventScope.Application, bool excludeDeployments = false)
                {
                    return Format("/events/listevents/{id}", new object[] { }, new Dictionary<string, object>() { { "documentIds", documentIds }, { "scope", scope }, { "excludeDeployments", excludeDeployments } });
                }

            }
            public static class ReferenceData
            {
                /// <summary>
                /// Returns a URI like: /referencedata/data
                /// </summary>
                public static string Data()
                {
                    return Format("/referencedata/data/{id}", new object[] { }, new Dictionary<string, object>() { });
                }

            }
            public static class Deployments
            {
                /// <summary>
                /// Returns a URI like: /projects/Foo/releases/Foo/deployments
                /// </summary>
                public static string Index(string slug, string releaseVersion)
                {
                    return Format("/projects/{0}/releases/{1}/deployments", new object[] { slug, releaseVersion }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /projects/Foo/releases/Foo/deployments/Foo
                /// </summary>
                public static string Show(string slug, string releaseVersion, string id)
                {
                    return Format("/projects/{0}/releases/{1}/deployments/{2}", new object[] { slug, releaseVersion, id }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /projects/Foo/releases/Foo/deployments/Foo/details
                /// </summary>
                public static string Details(string slug, string releaseVersion, string id)
                {
                    return Format("/projects/{0}/releases/{1}/deployments/{2}/details", new object[] { slug, releaseVersion, id }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /projects/Foo/releases/Foo/deployments/create?templateid=Foo&amp;force=False
                /// </summary>
                public static string Create(string slug, string releaseVersion, string templateId, bool force = false)
                {
                    return Format("/projects/{0}/releases/{1}/deployments/create", new object[] { slug, releaseVersion }, new Dictionary<string, object>() { { "templateId", templateId }, { "force", force } });
                }

            }
            public static class Master
            {
                /// <summary>
                /// Returns a URI like: /master/listprojects
                /// </summary>
                public static string ListProjects()
                {
                    return Format("/master/listprojects/{id}", new object[] { }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /master/showavailableupgrade
                /// </summary>
                public static string ShowAvailableUpgrade()
                {
                    return Format("/master/showavailableupgrade/{id}", new object[] { }, new Dictionary<string, object>() { });
                }

            }
            public static class Projects
            {
                /// <summary>
                /// Returns a URI like: /projects
                /// </summary>
                public static string Index()
                {
                    return Format("/projects", new object[] { }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /projects/Foo
                /// </summary>
                public static string Show(string slug)
                {
                    return Format("/projects/{0}", new object[] { slug }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /projects/Foo/edit?defaultgroupid=Foo&amp;cloneprojectid=Foo
                /// </summary>
                public static string Edit(string slug, string defaultGroupId = null, string cloneProjectId = null)
                {
                    return Format("/projects/{0}/edit", new object[] { slug }, new Dictionary<string, object>() { { "defaultGroupId", defaultGroupId }, { "cloneProjectId", cloneProjectId } });
                }

                /// <summary>
                /// Returns a URI like: /projects/Foo/disabled
                /// </summary>
                public static string Disabled(string slug)
                {
                    return Format("/projects/{0}/disabled", new object[] { slug }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /projects/menu/Foo?currenttab=Foo
                /// </summary>
                public static string Menu(string id, string currentTab)
                {
                    return Format("/projects/menu/{0}", new object[] { id }, new Dictionary<string, object>() { { "currentTab", currentTab } });
                }

            }
            public static class Steps
            {
                /// <summary>
                /// Returns a URI like: /projects/Foo/steps
                /// </summary>
                public static string Index(string slug)
                {
                    return Format("/projects/{0}/steps/index/{id}", new object[] { slug }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /projects/Foo/steps/add
                /// </summary>
                public static string Add(string slug)
                {
                    return Format("/projects/{0}/steps/add/{id}", new object[] { slug }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /projects/Foo/steps/packagestep/Foo
                /// </summary>
                public static string PackageStep(string slug, string id)
                {
                    return Format("/projects/{0}/steps/packagestep/{1}", new object[] { slug, id }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /projects/Foo/steps/manualstep/Foo
                /// </summary>
                public static string ManualStep(string slug, string id)
                {
                    return Format("/projects/{0}/steps/manualstep/{1}", new object[] { slug, id }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /projects/Foo/steps/scriptstep/Foo
                /// </summary>
                public static string ScriptStep(string slug, string id)
                {
                    return Format("/projects/{0}/steps/scriptstep/{1}", new object[] { slug, id }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /projects/Foo/steps/ftpstep/Foo
                /// </summary>
                public static string FtpStep(string slug, string id)
                {
                    return Format("/projects/{0}/steps/ftpstep/{1}", new object[] { slug, id }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /projects/Foo/steps/azurestep/Foo
                /// </summary>
                public static string AzureStep(string slug, string id)
                {
                    return Format("/projects/{0}/steps/azurestep/{1}", new object[] { slug, id }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /projects/Foo/steps/emailstep/Foo
                /// </summary>
                public static string EmailStep(string slug, string id)
                {
                    return Format("/projects/{0}/steps/emailstep/{1}", new object[] { slug, id }, new Dictionary<string, object>() { });
                }

            }
            public static class Variables
            {
                /// <summary>
                /// Returns a URI like: /projects/Foo/variables
                /// </summary>
                public static string Index(string slug)
                {
                    return Format("/projects/{0}/variables/index/{id}", new object[] { slug }, new Dictionary<string, object>() { });
                }

            }
            public static class Dashboard
            {
                /// <summary>
                /// Returns a URI like: /
                /// </summary>
                public static string Index()
                {
                    return Format("/", new object[] { }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /dashboard/licenserequired
                /// </summary>
                public static string LicenseRequired()
                {
                    return Format("/dashboard/licenserequired/{id}", new object[] { }, new Dictionary<string, object>() { });
                }
            }
            public static class Environments
            {
                /// <summary>
                /// Returns a URI like: /environments
                /// </summary>
                public static string Index()
                {
                    return Format("/environments/index/{id}", new object[] { }, new Dictionary<string, object>() { });
                }

            }
            public static class Releases
            {
                /// <summary>
                /// Returns a URI like: /projects/Foo/releases?releaseversion=Foo&amp;skip=0&amp;take=0
                /// </summary>
                public static string Index(string slug, string releaseVersion, int skip = 0, int take = 10)
                {
                    return Format("/projects/{0}/releases", new object[] { slug }, new Dictionary<string, object>() { { "releaseVersion", releaseVersion }, { "skip", skip }, { "take", take } });
                }

                /// <summary>
                /// Returns a URI like: /projects/Foo/releases/Foo
                /// </summary>
                public static string Show(string slug, string releaseVersion)
                {
                    return Format("/projects/{0}/releases/{1}", new object[] { slug, releaseVersion }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /projects/Foo/releases/edit/Foo
                /// </summary>
                public static string Edit(string slug, string releaseVersion)
                {
                    return Format("/projects/{0}/releases/edit/{1}", new object[] { slug, releaseVersion }, new Dictionary<string, object>() { });
                }

            }
            public static class Resource
            {
                /// <summary>
                /// Returns a URI like: /resource?name=Foo&amp;mime=Foo
                /// </summary>
                public static string Index(string name, string mime)
                {
                    return Format("/resource/index/{id}", new object[] { }, new Dictionary<string, object>() { { "name", name }, { "mime", mime } });
                }

                /// <summary>
                /// Returns a URI like: /resource/publiccertificate
                /// </summary>
                public static string PublicCertificate()
                {
                    return Format("/resource/publiccertificate/{id}", new object[] { }, new Dictionary<string, object>() { });
                }

            }
            public static class Tasks
            {
                /// <summary>
                /// Returns a URI like: /tasks
                /// </summary>
                public static string Index(int? skip = 0)
                {
                    return Format("/tasks/index/{id}", new object[] { }, new Dictionary<string, object>() { { "skip", skip } });
                }

                /// <summary>
                /// Returns a URI like: /tasks/show/Foo
                /// </summary>
                public static string Show(string id)
                {
                    return Format("/tasks/{0}", new object[] { id }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /tasks/details/Foo
                /// </summary>
                public static string Details(string id)
                {
                    return Format("/tasks/details/{0}", new object[] { id }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /tasks/output/Foo
                /// </summary>
                public static string Output(string id)
                {
                    return Format("/tasks/output/{0}", new object[] { id }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /tasks/outputastext/Foo
                /// </summary>
                public static string OutputAsText(string id)
                {
                    return Format("/tasks/outputastext/{0}", new object[] { id }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /tasks/cancel/Foo
                /// </summary>
                public static string Cancel(string id)
                {
                    return Format("/tasks/cancel/{0}", new object[] { id }, new Dictionary<string, object>() { });
                }

                /// <summary>
                /// Returns a URI like: /tasks/tryagain/Foo
                /// </summary>
                public static string TryAgain(string id)
                {
                    return Format("/tasks/tryagain/{0}", new object[] { id }, new Dictionary<string, object>() { });
                }

            }
        }
    }
}