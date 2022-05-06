using System;

namespace Octopus.Shared.Configuration.EnvironmentVariableMappings
{
    public class EnvironmentVariable : IComparable
    {
        public EnvironmentVariable(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public static EnvironmentVariable PlaintText(string name)
            => new EnvironmentVariable(name);

        public static SensitiveEnvironmentVariable Sensitive(string name, string sensitiveWarningDescription)
            => new SensitiveEnvironmentVariable(name, sensitiveWarningDescription);

        protected bool Equals(EnvironmentVariable other)
            => Name == other.Name;

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != GetType())
                return false;
            return Equals((EnvironmentVariable)obj);
        }

        public override int GetHashCode()
            => Name.GetHashCode();

        public int CompareTo(object? obj)
        {
            var v = obj as EnvironmentVariable;
            if (v == null)
                return -1;
            return Name.CompareTo(v.Name);
        }
    }

    public class SensitiveEnvironmentVariable : EnvironmentVariable
    {
        public SensitiveEnvironmentVariable(string name, string warningDescription) : base(name)
        {
            WarningDescription = warningDescription;
        }

        public string? WarningDescription { get; }
    }
}