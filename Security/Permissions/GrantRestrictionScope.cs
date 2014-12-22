using System;
using System.Collections.Generic;
using Octopus.Client.Model;
using Octopus.Platform.Model;

namespace Octopus.Platform.Security.Permissions
{
    public class GrantRestrictionScope
    {
        readonly string scopeGroup;
        readonly ReferenceCollection documentIds;

        public GrantRestrictionScope(string scopeGroup, IEnumerable<string> documentIds)
        {
            this.scopeGroup = scopeGroup;
            this.documentIds = new ReferenceCollection(documentIds);
            if (this.documentIds.Count == 0)
                throw new ArgumentException("Restrictions must contain at least one document");
        }

        public string ScopeGroup
        {
            get { return scopeGroup; }
        }

        public ReferenceCollection DocumentIds { get { return documentIds; } }

        public bool Allows(string documentId)
        {
            if (documentId == null) throw new ArgumentNullException("documentId");
            return documentIds.Contains(documentId);
        }
    }
}