using System;
using System.Collections.Generic;
using Octopus.Shared.Util;

namespace Octopus.Shared.Integration.PowerShell
{
    public class PowerShellArguments : RemotedObject
    {
        public PowerShellArguments()
        {
            OutputStream = new PowerShellOutput();
        }

        public string ScriptFilePath { get; set; }
        public string WorkingDirectory { get; set; }
        public IDictionary<string, string> Variables { get; set; }
        public PowerShellOutput OutputStream { get; private set; }
    }
}