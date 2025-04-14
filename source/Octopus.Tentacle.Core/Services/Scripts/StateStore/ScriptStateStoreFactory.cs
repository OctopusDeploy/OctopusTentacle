using System;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Core.Services.Scripts.StateStore
{
    public class ScriptStateStoreFactory : IScriptStateStoreFactory
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