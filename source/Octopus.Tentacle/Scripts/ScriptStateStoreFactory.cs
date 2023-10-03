using System;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Scripts
{
    internal class ScriptStateStoreFactory : IScriptStateStoreFactory
    {
        readonly IOctopusFileSystem fileSystem;

        public ScriptStateStoreFactory(IOctopusFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public ScriptStateStore Create(IScriptWorkspace workspace)
        {
            return new ScriptStateStore(workspace, fileSystem);
        }
    }
}