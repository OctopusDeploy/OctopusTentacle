using System;
using System.Collections.Generic;
using Octopus.Client.Model;

namespace Octopus.Shared.Security.Permissions
{
    public interface IPermissionSet
    {
        AuthorizationResult CheckAuthorized(Permission permission, string userId, string[] externalSecurityGroupIds, AuthorizationRequest request);
        IList<RestrictedGrant> GetRestrictions(Permission permission, string userId, string[] externalSecurityGroupIds);
        ReferenceCollection SuggestTeams(Permission permission, AuthorizationRequest request);
    }
}