using System;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Commands
{
    /// <summary>
    /// Shared definition of the "polling connection count" command-line option so that the <c>configure</c> and
    /// <c>set-polling-connection-count</c> commands expose the same option name and apply identical parsing and
    /// validation.
    /// </summary>
    internal static class PollingConnectionCountOption
    {
        public const string Name = "pollingConnectionCount";

        public const string Description = "The number of polling connections this Tentacle should open to each Octopus Server it polls. Only applies to polling Tentacles.";

        /// <summary>
        /// The option as it should be passed to <c>OptionSet.Add</c> (i.e. expecting a value).
        /// </summary>
        public const string Prototype = Name + "=";

        /// <summary>
        /// Parses and validates a user-supplied polling connection count, throwing a friendly
        /// <see cref="ControlledFailureException"/> if the value cannot be parsed or is out of range.
        /// </summary>
        public static int Parse(string value)
        {
            if (!int.TryParse(value, out var count))
                throw new ControlledFailureException($"The polling connection count '{value}' is not a valid whole number. Specify a positive integer, e.g. --{Name}=5");

            if (count < 1)
                throw new ControlledFailureException($"The polling connection count must be greater than 0, but was {count}.");

            return count;
        }
    }
}
