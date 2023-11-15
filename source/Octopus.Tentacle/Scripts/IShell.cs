using System;

namespace Octopus.Tentacle.Scripts
{
    public interface IShell
    {
        string Name { get; }
        string GetFullPath();

        string FormatCommandArguments(string bootstrapFile, string[]? scriptArguments, bool allowInteractive);
    }
}