using System;

namespace Octopus.Shared.Activities
{
    public abstract class ActivityLogDecorator : AbstractActivityLog
    {
        readonly IActivityLog inner;

        protected ActivityLogDecorator(IActivityLog inner)
        {
            this.inner = inner;
        }

        public override void Write(ActivityLogLevel level, object message)
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
}