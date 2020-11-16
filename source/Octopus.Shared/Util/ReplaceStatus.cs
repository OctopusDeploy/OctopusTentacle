using System;

namespace Octopus.Shared.Util
{
    public struct ReplaceStatus : IEquatable<ReplaceStatus>
    {
        public static readonly ReplaceStatus Created = new ReplaceStatus("Created");
        public static readonly ReplaceStatus Deleted = new ReplaceStatus("Deleted");
        public static readonly ReplaceStatus Updated = new ReplaceStatus("Updated");
        public static readonly ReplaceStatus SkippedKeyCollision = new ReplaceStatus("Skipped (key collision)");
        public static readonly ReplaceStatus SkippedIncomplete = new ReplaceStatus("Skipped (incomplete)");
        public static readonly ReplaceStatus SkippedTooOld = new ReplaceStatus("Skipped (maxage)");
        public static readonly ReplaceStatus SkippedNoOverwrite = new ReplaceStatus("Skipped (no overwrite)");
        public static readonly ReplaceStatus SkippedNull = new ReplaceStatus("Skipped (null)");
        public static readonly ReplaceStatus Unchanged = new ReplaceStatus("Unchanged");
        public static readonly ReplaceStatus MissingDependencies = new ReplaceStatus("Missing dependencies");
        public static readonly ReplaceStatus PropertyAdded = new ReplaceStatus("Property added");
        public static readonly ReplaceStatus PropertyDeleted = new ReplaceStatus("Property deleted");
        public static readonly ReplaceStatus PropertyUnchanged = new ReplaceStatus("Property unchanged");
        public static readonly ReplaceStatus PropertyUpdated = new ReplaceStatus("Property updated");
        public static readonly ReplaceStatus Error = new ReplaceStatus("Error");
        public readonly string Description;

        public ReplaceStatus(string description)
        {
            Description = description;
        }

        public bool Equals(ReplaceStatus other)
            => Description == other.Description;

        public override string ToString()
            => Description;
    }
}