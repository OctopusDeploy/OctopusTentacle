using System;

namespace Octopus.Shared.Integration.Scripting
{
    public interface IScriptRunner
    {
        string[] GetSupportedExtensions();
        ScriptExecutionResult Execute(ScriptArguments arguments);
    }
}