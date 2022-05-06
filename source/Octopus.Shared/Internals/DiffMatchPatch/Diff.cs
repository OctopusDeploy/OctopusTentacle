using System;

namespace Octopus.Shared.Internals.DiffMatchPatch
{
    public class Diff
    {
        public Operation operation;

        // One of: INSERT, DELETE or EQUAL.
        public string text;
        // The text associated with this diff operation.

        /**
         * Constructor.  Initializes the diff with the provided values.
         * @param operation One of INSERT, DELETE or EQUAL.
         * @param text The text being applied.
         */
        public Diff(Operation operation, string text)
        {
            // Construct a diff with the specified operation and text.
            this.operation = operation;
            this.text = text;
        }

        /**
         * Display a human-readable version of this Diff.
         * @return text version.
         */
        public override string ToString()
        {
            var prettyText = text.Replace('\n', '\u00b6');
            return "Diff(" + operation + ",\"" + prettyText + "\")";
        }

        /**
         * Is this Diff equivalent to another Diff?
         * @param d Another Diff to compare against.
         * @return true or false.
         */
        public override bool Equals(object? obj)
        {
            // If parameter is null return false.
            if (obj == null)
                return false;

            // If parameter cannot be cast to Diff return false.
            var p = obj as Diff;
            if (p == null)
                return false;

            // Return true if the fields match.
            return p.operation == operation && p.text == text;
        }

        public bool Equals(Diff obj)
        {
            // If parameter is null return false.
            if (obj == null)
                return false;

            // Return true if the fields match.
            return obj.operation == operation && obj.text == text;
        }

        public override int GetHashCode()
            => text.GetHashCode() ^ operation.GetHashCode();
    }
}