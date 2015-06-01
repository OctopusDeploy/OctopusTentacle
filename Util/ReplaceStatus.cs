using System;

namespace Octopus.Shared.Util
{
    public struct ReplaceStatus : IEquatable<ReplaceStatus>
    {
        public readonly string Description;

        public ReplaceStatus(string description)
        {
            this.Description = description;
        }

        public static readonly ReplaceStatus Created = new ReplaceStatus("Created");
        public static readonly ReplaceStatus Deleted = new ReplaceStatus("Deleted");
        public static readonly ReplaceStatus Updated = new ReplaceStatus("Updated");
        public static readonly ReplaceStatus Skipped = new ReplaceStatus("Skipped");
        public static readonly ReplaceStatus Unchanged = new ReplaceStatus("Unchanged");
        public static readonly ReplaceStatus MissingDependencies = new ReplaceStatus("Missing dependencies");
        public static readonly ReplaceStatus PropertyAdded = new ReplaceStatus("Property added");
        public static readonly ReplaceStatus PropertyDeleted = new ReplaceStatus("Property deleted");
        public static readonly ReplaceStatus PropertyUnchanged = new ReplaceStatus("Property unchanged");
        public static readonly ReplaceStatus PropertyUpdated = new ReplaceStatus("Property updated");
        public static readonly ReplaceStatus Error = new ReplaceStatus("Error");

        public bool Equals(ReplaceStatus other)
        {
            return this.Description == other.Description;
        }

        public override string ToString()
        {
            return Description;
        }
    }
}
