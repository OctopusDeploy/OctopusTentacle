
using Octopus.Client.Model;
using System.Collections.Generic;

namespace Octopus.Shared.Security.Permissions
{
    public interface ITeamPermission
    {
        IEnumerable<RestrictedGrant> GrantForPermission(Permission permission);
    }
}
