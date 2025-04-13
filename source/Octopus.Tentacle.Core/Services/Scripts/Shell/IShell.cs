using System;

namespace Octopus.Tentacle.Core.Services.Scripts.Shell
{
    public interface IShell
    {
        string Name { get; }
        string GetFullPath();

        string FormatCommandArguments(string bootstrapFile, string[]? scriptArguments, bool allowInteractive);
    }
}