using System;
using Octopus.Shared.Diagnostics;
using log4net;

namespace Octopus.Shared.Activities
{
    public class NullActivityLog : IActivityLog
    {
        readonly ILog defaultLog;

        public NullActivityLog() : this(Logger.Default)
        {
        }

        public NullActivityLog(ILog defaultLog)
        {
            this.defaultLog = defaultLog;
        }

        public void Debug(object message)
        {
            defaultLog.Debug(message);
        }

        public void Debug(object message, Exception exception)
        {
            defaultLog.Debug(message, exception);
        }

        public void DebugFormat(string format, params object[] args)
        {
            defaultLog.DebugFormat(format, args);
        }

        public void Info(object message)
        {
            defaultLog.Info(message);
        }

        public void Info(object message, Exception exception)
        {
            defaultLog.Info(message, exception);
        }

        public void InfoFormat(string format, params object[] args)
        {
            defaultLog.InfoFormat(format, args);
        }

        public void Warn(object message)
        {
            defaultLog.Warn(message);
        }

        public void Warn(object message, Exception exception)
        {
            defaultLog.Warn(message, exception);
        }

        public void WarnFormat(string format, params object[] args)
        {
            defaultLog.WarnFormat(format, args);
        }

        public void Error(object message)
        {
            defaultLog.Error(message);
        }

        public void Error(object message, Exception exception)
        {
            defaultLog.Error(message, exception);
        }

        public void ErrorFormat(string format, params object[] args)
        {
            defaultLog.ErrorFormat(format, args);
        }

        public IActivityLog OverwritePrevious()
        {
            return this;
        }

        public string GetLog()
        {
            return string.Empty;
        }
    }
}