﻿using System.Threading;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;

namespace Octopus.Tentacle.Scripts
{
    public interface IScriptExecutor
    {
        IRunningScript ExecuteOnBackgroundThread(StartScriptCommandV3Alpha command, IScriptWorkspace workspace, ScriptStateStore? scriptStateStore, CancellationToken cancellationToken);
    }
}