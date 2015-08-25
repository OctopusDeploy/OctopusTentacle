using System;

namespace Octopus.Shared.TransientFaultHandling
{
    /// <summary>
    /// Implements a strategy that ignores any transient errors.
    /// </summary>
    sealed class TransientErrorIgnoreStrategy : ITransientErrorDetectionStrategy
    {
        /// <summary>
        /// Always returns false.
        /// </summary>
        /// <param name="ex">The exception.</param>
        /// <returns>Always false.</returns>
        public bool IsTransient(Exception ex)
        {
            return false;
        }
    }
}