using System;
using Octopus.Client.Model;

namespace Octopus.Platform.Security.Permissions
{
    public static class WildcardDocumentId
    {
        public const string Identifier = "*";
        const string WildcardSuffix = "-" + Identifier;
        public const string AnyEnvironment = PermissionScope.Environments + WildcardSuffix;
        public const string AnyProject = PermissionScope.Projects + WildcardSuffix;
    }
}