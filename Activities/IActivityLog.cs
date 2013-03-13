using System;
using log4net.Core;

namespace Octopus.Shared.Activities
{
    public interface IActivityLog
    {
        void Debug(object message);
        void Debug(object message, Exception exception);
        void DebugFormat(string format, params object[] args);

        void Info(object message);
        void Info(object message, Exception exception);
        void InfoFormat(string format, params object[] args);

        void Warn(object message);
        void Warn(object message, Exception exception);
        void WarnFormat(string format, params object[] args);

        void Error(object message);
        void Error(object message, Exception exception);
        void ErrorFormat(string format, params object[] args);

        void Write(Level level, object message);

        IActivityLog OverwritePrevious();

        string GetLog();
    }
}