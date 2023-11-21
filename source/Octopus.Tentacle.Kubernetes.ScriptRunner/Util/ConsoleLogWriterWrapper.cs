using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Scripts;

namespace Octopus.Tentacle.Kubernetes.ScriptRunner.Util;

class ConsoleLogWriterWrapper : IScriptLogWriter
{
    readonly IScriptLogWriter writer;

    public ConsoleLogWriterWrapper(IScriptLogWriter writer)
    {
        this.writer = writer;
    }

    public void WriteOutput(ProcessOutputSource source, string message)
    {
        Console.WriteLine($"[{source}] {message}");
        writer.WriteOutput(source, message);
    }

    public void Dispose()
    {
        writer.Dispose();
    }
}