using System;
using Octopus.Tentacle.Contracts;
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

        public ScriptStateStore Get(ScriptTicket ticket, IScriptWorkspace workspace)
        {
            return new ScriptStateStore(ticket, workspace, fileSystem);
        }
    }
}