using System;
using System.Collections.Generic;
using Octopus.Diagnostics;

namespace Octopus.Shared.Diagnostics
{
    public class DelayedLog : ISystemLog
    {
        readonly List<Action<ILog>> actions = new List<Action<ILog>>();

        public DelayedLog()
        {
            CorrelationId = Guid.NewGuid().ToString();
        }

        public string CorrelationId { get; }

        public void Dispose()
        {
        }

        public ISystemLog ChildContext(string[] sensitiveValues)
        {
            return new DelayedLog();
        }

        public void WithSensitiveValues(string[] sensitiveValues)
        {
        }

        public void WithSensitiveValue(string sensitiveValue)
        {
        }

        public void Add(Action<ILog> action) => actions.Add(action);

        public void FlushTo(ILog log)
        {
            foreach (var action in actions)
                action(log);
        }

        public void Trace(string messageText) => Add(l => l.Trace(messageText));
        public void Trace(Exception error) => Add(l => l.Trace(error));
        public void Trace(Exception error, string messageText) => Add(l => l.Trace(error, messageText));

        public void Verbose(string messageText) => Add(l => l.Verbose(messageText));
        public void Verbose(Exception error) => Add(l => l.Verbose(error));
        public void Verbose(Exception error, string messageText) => Add(l => l.Verbose(error, messageText));

        public void Info(string messageText) => Add(l => l.Info(messageText));
        public void Info(Exception error) => Add(l => l.Info(error));
        public void Info(Exception error, string messageText) => Add(l => l.Info(error, messageText));

        public void Warn(string messageText) => Add(l => l.Warn(messageText));
        public void Warn(Exception error) => Add(l => l.Warn(error));
        public void Warn(Exception error, string messageText) => Add(l => l.Warn(error, messageText));

        public void Error(string messageText) => Add(l => l.Error(messageText));
        public void Error(Exception error) => Add(l => l.Error(error));
        public void Error(Exception error, string messageText) => Add(l => l.Error(error, messageText));

        public void Fatal(string messageText) => Add(l => l.Fatal(messageText));
        public void Fatal(Exception error) => Add(l => l.Fatal(error));
        public void Fatal(Exception error, string messageText) => Add(l => l.Fatal(error, messageText));

        public void Write(LogCategory category, string messageText) => Add(l => l.Write(category, messageText));

        public void Write(LogCategory category, Exception error) => Add(l => l.Write(category, error));

        public void Write(LogCategory category, Exception error, string messageText) => Add(l => l.Write(category, error, messageText));

        public void WriteFormat(LogCategory category, string messageFormat, params object[] args) => Add(l => l.WriteFormat(category, messageFormat, args));

        public void WriteFormat(LogCategory category, Exception error, string messageFormat, params object[] args) => Add(l => l.WriteFormat(category, error, messageFormat, args));

        public void TraceFormat(string messageFormat, params object[] args) => Add(l => l.TraceFormat(messageFormat, args));
        public void TraceFormat(Exception error, string format, params object[] args) => Add(l => l.TraceFormat(error, format, args));

        public void VerboseFormat(string messageFormat, params object[] args) => Add(l => l.VerboseFormat(messageFormat, args));
        public void VerboseFormat(Exception error, string format, params object[] args) => Add(l => l.VerboseFormat(error, format, args));

        public void InfoFormat(string messageFormat, params object[] args) => Add(l => l.InfoFormat(messageFormat, args));
        public void InfoFormat(Exception error, string format, params object[] args) => Add(l => l.InfoFormat(error, format, args));

        public void WarnFormat(string messageFormat, params object[] args) => Add(l => l.WarnFormat(messageFormat, args));
        public void WarnFormat(Exception error, string format, params object[] args) => Add(l => l.WarnFormat(error, format, args));

        public void ErrorFormat(string messageFormat, params object[] args) => Add(l => l.ErrorFormat(messageFormat, args));
        public void ErrorFormat(Exception error, string format, params object[] args) => Add(l => l.ErrorFormat(error, format, args));

        public void FatalFormat(string messageFormat, params object[] args) => Add(l => l.FatalFormat(messageFormat, args));
        public void FatalFormat(Exception error, string format, params object[] args) => Add(l => l.FatalFormat(error, format, args));

        public void Flush() => Add(l => l.Flush());
    }
}