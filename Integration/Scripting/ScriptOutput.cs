using System;
using Octopus.Platform.Util;

namespace Octopus.Shared.Integration.Scripting
{
    public class ScriptOutput : RemotedObject
    {
        public event Action<string> Written;

        public void OnWritten(string message)
        {
            var handler = Written;
            if (handler != null) handler(message);
        }
    }
}