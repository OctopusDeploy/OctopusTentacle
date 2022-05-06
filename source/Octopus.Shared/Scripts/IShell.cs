using System;

namespace Octopus.Shared.Scripts
{
    public interface IShell
    {
        string GetFullPath();

        string FormatCommandArguments(string bootstrapFile, string[]? scriptArguments, bool allowInteractive);
    }
}