using System;

namespace Octopus.Shared.Util
{
    public enum ReplaceStatus
    {
        Created,
        Deleted,
        Updated,
        Skipped,
        MissingDependencies,
        PropertyAdded,
        PropertyDeleted,
        PropertySkipped,
        PropertyUpdated
    }
}
