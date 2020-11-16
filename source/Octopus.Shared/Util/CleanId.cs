using System;

namespace Octopus.Shared.Util
{
    /// <summary>
    /// A string guaranteed to hold a trimmed, lowercase, non-whitespace value.
    /// </summary>
    public struct CleanId
    {
        readonly string? value;

        public CleanId(string value)
        {
            var clean = value != null ? value.Trim() : null;

            this.value = string.IsNullOrWhiteSpace(clean) ? null : clean.ToLowerInvariant();
        }

        public bool HasValue => value != null;

        public string Value
        {
            get
            {
                if (value == null) throw new InvalidOperationException("The id has no value");
                return value;
            }
        }

        public static implicit operator string?(CleanId @this)
            => @this.value;
    }
}