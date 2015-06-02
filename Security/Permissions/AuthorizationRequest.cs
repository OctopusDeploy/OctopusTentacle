using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Shared.Security.Permissions
{
    public class AuthorizationRequest
    {
        readonly Dictionary<string, AuthorizationScopeAssertion> scopeAssertions = new Dictionary<string, AuthorizationScopeAssertion>(StringComparer.InvariantCultureIgnoreCase);

        public AuthorizationRequest(IEnumerable<string> regardingDocumentIds)
        {
            foreach (var regardingDocumentId in regardingDocumentIds)
            {
                var scope = new AuthorizationScopeAssertion(regardingDocumentId);
                if (scopeAssertions.ContainsKey(scope.ScopeGroup))
                    throw new ArgumentException("Duplicate assertions have been specified for scope group '" + scope.ScopeGroup + "'. This isn't supported; loop through and make multiple checks instead.");
                scopeAssertions.Add(scope.ScopeGroup, scope);
            }
        }

        public bool SupportsRestrictions(RestrictedGrant restrictions)
        {
            foreach (var restrictionScope in restrictions.Scopes)
            {
                if (!scopeAssertions.ContainsKey(restrictionScope))
                    return false;
            }

            foreach (var assertion in scopeAssertions.Values.Where(a => !a.IsWildcard))
            {
                if (!restrictions.Allow(assertion))
                    return false;
            }

            return true;
        }
    }
}