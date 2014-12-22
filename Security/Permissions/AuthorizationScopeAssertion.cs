using System;

namespace Octopus.Platform.Security.Permissions
{
    public class AuthorizationScopeAssertion
    {
        readonly string scopeDocumentId;
        readonly string scopeGroup;
        readonly bool isWildcard;

        public AuthorizationScopeAssertion(string scopeDocumentId)
        {
            string suffix;
            DocumentIdParser.Split(scopeDocumentId, out scopeGroup, out suffix);
            if (suffix == WildcardDocumentId.Identifier)
                isWildcard = true;
            else
                this.scopeDocumentId = scopeDocumentId;
        }

        public string ScopeGroup
        {
            get { return scopeGroup; }
        }

        public bool IsWildcard
        {
            get { return isWildcard; }
        }

        public string DocumentId
        {
            get 
            {
                if (isWildcard)
                    throw new InvalidOperationException("The scope is a wildcard");

                return scopeDocumentId;
            }
        }
    }
}