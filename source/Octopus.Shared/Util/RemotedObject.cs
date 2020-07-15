using System;

namespace Octopus.Shared.Util
{
    public class RemotedObject : MarshalByRefObject
    {
        public override sealed object? InitializeLifetimeService()
        {
            // Live forever
            return null;
        }
    }
}