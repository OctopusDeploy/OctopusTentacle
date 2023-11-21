using Octopus.Tentacle.Diagnostics;

namespace Octopus.Tentacle.Kubernetes.ScriptRunner.Diagnostics;

public class ConsoleLogAppender : ILogAppender
{
    public void WriteEvent(LogEvent logEvent)
    {
        var exceptionMessage = logEvent.Error is not null ? $"{Environment.NewLine}{logEvent.Error}" : string.Empty;
        var message = $"{DateTime.UtcNow:O} {logEvent.Category.ToString().ToUpperInvariant()} {logEvent.MessageText}{exceptionMessage}";

        if (logEvent.Error is not null)
        {
            Console.Error.WriteLine(message);
        }
        else
        {
            Console.Out.WriteLine(message);
        }
    }

    public void Flush()
    {
        Console.Error.Flush();
        Console.Out.Flush();
    }
}