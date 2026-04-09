using System;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Core.Services.Scripts.WorkSpace
{
    public static class ScriptWorkspaceTypeFromOs
    {
        public static ScriptWorkspaceType ForCurrentOs()
        {
            return PlatformDetection.IsRunningOnWindows ? ScriptWorkspaceType.PowerShell : ScriptWorkspaceType.Bash;
        }
    }
    
    public enum ScriptWorkspaceType
    {
        Bash,
        PowerShell
    }
}