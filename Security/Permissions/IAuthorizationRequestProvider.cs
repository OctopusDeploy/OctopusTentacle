using System;
using System.Collections.Generic;
using Octopus.Client.Model;

namespace Octopus.Shared.Security.Permissions
{
    public interface IAuthorizationRequestProvider
    {
        IEnumerable<AuthorizationRequest> GetForPermission(Permission permission, string[] explicitDocumentIds);
    }
}