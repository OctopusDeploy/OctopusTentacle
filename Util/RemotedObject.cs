using System;

namespace Octopus.Platform.Util
{
    public class RemotedObject : MarshalByRefObject
    {
        public override sealed object InitializeLifetimeService()
        {
            // Live forever
            return null;
        }
    }
}