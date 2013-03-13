using System;
using Octopus.Shared.Util;

namespace Octopus.Shared.Integration.PowerShell
{
    public class PowerShellOutput : RemotedObject
    {
        public event Action<string> Written;

        public void OnWritten(string message)
        {
            var handler = Written;
            if (handler != null) handler(message);
        }
    }
}