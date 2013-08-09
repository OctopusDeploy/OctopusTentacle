using System;
using Octopus.Shared.Orchestration.Logging;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Activities
{
    public abstract class ActivityLogDecorator : AbstractActivityLog
    {
        readonly ITrace inner;

        protected ActivityLogDecorator(ITrace inner)
        {
            this.inner = inner;
        }

        public override void Write(TraceCategory level, object message)
        {
            inner.Write(level, (message ?? "").ToString());
        }
    }
}