using System;
using Octopus.Shared.Util;
using log4net.Core;

namespace Octopus.Shared.Activities
{
    public class PrefixedActivityLogDecorator : ActivityLogDecorator
    {
        readonly string prefix;

        public PrefixedActivityLogDecorator(string prefix, IActivityLog inner) : base(inner)
        {
            this.prefix = prefix;
        }

        public override void Write(Level level, object message)
        {
            base.Write(level, prefix + message);
        }
    }

    public abstract class ActivityLogDecorator : AbstractActivityLog
    {
        readonly IActivityLog inner;

        protected ActivityLogDecorator(IActivityLog inner)
        {
            this.inner = inner;
        }

        public override void Write(Level level, object message)
        {
            inner.Write(level, message);
        }

        public override IActivityLog OverwritePrevious()
        {
            inner.OverwritePrevious();
            return this;
        }

        public override string GetLog()
        {
            return inner.GetLog();
        }
    }

    public abstract class AbstractActivityLog : IActivityLog
    {
        public void Debug(object message)
        {
            Write(Level.Debug, message);
        }

        public void Debug(object message, Exception exception)
        {
            Write(Level.Debug, message, exception);
        }

        public void DebugFormat(string format, params object[] args)
        {
            Write(Level.Debug, format, args);
        }

        public void Info(object message)
        {
            Write(Level.Info, message);
        }

        public void Info(object message, Exception exception)
        {
            Write(Level.Info, message, exception);
        }

        public void InfoFormat(string format, params object[] args)
        {
            Write(Level.Info, format, args);
        }

        public void Warn(object message)
        {
            Write(Level.Warn, message);
        }

        public void Warn(object message, Exception exception)
        {
            Write(Level.Warn, message, exception);
        }

        public void WarnFormat(string format, params object[] args)
        {
            Write(Level.Warn, format, args);
        }

        public void Error(object message)
        {
            Write(Level.Error, message);
        }

        public void Error(object message, Exception exception)
        {
            Write(Level.Error, message, exception);
        }

        public void ErrorFormat(string format, params object[] args)
        {
            Write(Level.Error, format, args);
        }

        public abstract void Write(Level level, object message);

        void Write(Level level, string format, object[] args)
        {
            var message = string.Format(format, args);
            Write(level, message);
        }

        void Write(Level level, object message, Exception ex)
        {
            Write(level, message + " " + ex.GetRootError());
        }

        public abstract IActivityLog OverwritePrevious();

        public abstract string GetLog();
    }
}