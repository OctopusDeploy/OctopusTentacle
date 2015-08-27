using System;

namespace Octopus.Shared.TransientFaultHandling
{
    /// <summary>
    /// Implements a strategy that treats all exceptions as transient errors.
    /// </summary>
    sealed class TransientErrorCatchAllStrategy : ITransientErrorDetectionStrategy
    {
        /// <summary>
        /// Always returns true.
        /// </summary>
        /// <param name="ex">The exception.</param>
        /// <returns>Always true.</returns>
        public bool IsTransient(Exception ex)
        {
            return true;
        }
    }
}