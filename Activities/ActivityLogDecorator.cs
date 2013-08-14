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

        protected override void WriteEvent(TraceCategory category, Exception error, string messageText)
        {
            var message = messageText;

            if (error != null)
            {
                if (message != null)
                    message += " " + error;
                else
                    message = error.ToString();
            }

            inner.Write(category, message);
        }
    }
}