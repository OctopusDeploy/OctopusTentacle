using System;

namespace Octopus.Tentacle.Client.Scripts
{
    public class UnsafeStartAttemptException : Exception
    {
        public UnsafeStartAttemptException(string message) : base(message)
        {
            
        }
    }
}