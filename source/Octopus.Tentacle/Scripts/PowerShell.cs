using System;

namespace Octopus.Tentacle.Scripts
{
    public class PowerShell : IShell
    {
        readonly IShell inner;
        public string Name => nameof(PowerShell);

        public PowerShell()
        {
            var powerShellCore = new PowerShellCore();
            if (powerShellCore.PowerShellCoreExists)
                inner = powerShellCore;
            else
                inner = new PowerShellDesktop();
        }

        public string GetFullPath() => inner.GetFullPath();

        public string FormatCommandArguments(string bootstrapFile, string[]? scriptArguments, bool allowInteractive)
            => inner.FormatCommandArguments(bootstrapFile, scriptArguments, allowInteractive);
    }
}
