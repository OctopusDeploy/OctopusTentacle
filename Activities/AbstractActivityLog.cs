using System;
using Octopus.Shared.Util;

namespace Octopus.Shared.Activities
{
    public abstract class AbstractActivityLog : IActivityLog
    {
        public void Debug(object message)
        {
            Write(ActivityLogLevel.Debug, message);
        }

        public void Debug(object message, Exception exception)
        {
            Write(ActivityLogLevel.Debug, message, exception);
        }

        public void DebugFormat(string format, params object[] args)
        {
            Write(ActivityLogLevel.Debug, format, args);
        }

        public void Info(object message)
        {
            Write(ActivityLogLevel.Info, message);
        }

        public void Info(object message, Exception exception)
        {
            Write(ActivityLogLevel.Info, message, exception);
        }

        public void InfoFormat(string format, params object[] args)
        {
            Write(ActivityLogLevel.Info, format, args);
        }

        public void Warn(object message)
        {
            Write(ActivityLogLevel.Warn, message);
        }

        public void Warn(object message, Exception exception)
        {
            Write(ActivityLogLevel.Warn, message, exception);
        }

        public void WarnFormat(string format, params object[] args)
        {
            Write(ActivityLogLevel.Warn, format, args);
        }

        public void Error(object message)
        {
            Write(ActivityLogLevel.Error, message);
        }

        public void Error(object message, Exception exception)
        {
            Write(ActivityLogLevel.Error, message, exception);
        }

        public void ErrorFormat(string format, params object[] args)
        {
            Write(ActivityLogLevel.Error, format, args);
        }

        public abstract void Write(ActivityLogLevel level, object message);

        void Write(ActivityLogLevel level, string format, object[] args)
        {
            var message = string.Format(format, args);
            Write(level, message);
        }

        void Write(ActivityLogLevel level, object message, Exception ex)
        {
            Write(level, message + " " + ex.GetRootError());
        }

        public abstract IActivityLog OverwritePrevious();

        public abstract string GetLog();
    }
}