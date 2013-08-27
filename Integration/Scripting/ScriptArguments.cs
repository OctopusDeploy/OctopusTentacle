using System;
using System.Collections.Generic;
using Octopus.Platform.Util;

namespace Octopus.Shared.Integration.Scripting
{
    public class ScriptArguments : RemotedObject
    {
        public ScriptArguments()
        {
            OutputStream = new ScriptOutput();
        }

        public string ScriptFilePath { get; set; }
        public string WorkingDirectory { get; set; }
        public IDictionary<string, string> Variables { get; set; }
        public ScriptOutput OutputStream { get; private set; }
    }
}