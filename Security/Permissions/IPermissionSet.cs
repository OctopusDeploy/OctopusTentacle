using System;
using System.Collections.Generic;
using Octopus.Client.Model;
using Octopus.Server.Extensibility.HostServices.Authentication;

namespace Octopus.Shared.Security.Permissions
{
    public interface IPermissionSet
    {
        AuthorizationResult CheckAuthorized(Permission permission, string userId, string[] externalSecurityGroupIds, string[] documentIds);
        IList<RestrictedGrant> GetRestrictions(Permission permission, string userId, string[] externalSecurityGroupIds);
        ReferenceCollection SuggestTeams(Permission permission, string[] documentIds);
    }
}