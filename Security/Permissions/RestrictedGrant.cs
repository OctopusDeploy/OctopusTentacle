using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Client.Model;

namespace Octopus.Shared.Security.Permissions
{
    public class RestrictedGrant
    {
        readonly Dictionary<string, GrantRestrictionScope> restrictions;

        public RestrictedGrant(params string[] restrictions)
            : this((IEnumerable<string>)restrictions)
        {
        }

        public RestrictedGrant(IEnumerable<string> restrictions)
        {
            if (restrictions == null) throw new ArgumentNullException("restrictions");
            this.restrictions = restrictions.Select(r =>
            {
                string scopeGroup, suffix;
                DocumentIdParser.Split(r, out scopeGroup, out suffix);
                return new {ScopeGroup = scopeGroup, DocumentId = r};
            })
                .GroupBy(r => r.ScopeGroup, StringComparer.OrdinalIgnoreCase)
                .Select(g => new GrantRestrictionScope(g.Key, g.Select(r => r.DocumentId)))
                .ToDictionary(grs => grs.ScopeGroup, StringComparer.OrdinalIgnoreCase);
        }

        public IEnumerable<string> Scopes
        {
            get { return restrictions.Keys; }
        }

        public IEnumerable<string> Restrictions
        {
            get { return restrictions.Values.SelectMany(r => r.DocumentIds); }
        }

        public bool Allow(AuthorizationScopeAssertion assertion)
        {
            if (assertion.IsWildcard)
                throw new ArgumentException("Didn't expect to check restrictions for a wildcard assertion");

            GrantRestrictionScope restriction;
            if (!restrictions.TryGetValue(assertion.ScopeGroup, out restriction))
                return true;

            return restriction.Allows(assertion.DocumentId);
        }

        public IEnumerable<string> Filter(IEnumerable<string> documentIds)
        {
            foreach (var documentId in documentIds)
            {
                string group, suffix;
                DocumentIdParser.Split(documentId, out group, out suffix);
                GrantRestrictionScope scope;
                if (!restrictions.TryGetValue(group, out scope) ||
                    scope.Allows(documentId))
                    yield return documentId;
            }
        }

        public ReferenceCollection For(string scope)
        {
            GrantRestrictionScope sc;
            if (!restrictions.TryGetValue(scope, out sc))
                return new ReferenceCollection();

            return sc.DocumentIds;
        }
    }
}