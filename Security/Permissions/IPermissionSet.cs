using System.Collections.Generic;
using Octopus.Client.Model;

namespace Octopus.Shared.Security.Permissions
{
    public interface IPermissionSet
    {
        AuthorizationResult CheckAuthorized(Permission permission, string userId, string[] externalSecurityGroupIds, string[] documentIds);
        IList<RestrictedGrant> GetRestrictions(Permission permission, string userId, string[] externalSecurityGroupIds);
        IList<RestrictedGrant> GetRestrictionsForTeamPermissions(Permission permission, IEnumerable<ITeamPermission> teamPermissions);
        ReferenceCollection SuggestTeams(Permission permission, string[] documentIds);
    }
}