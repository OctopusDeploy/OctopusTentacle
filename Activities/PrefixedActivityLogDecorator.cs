using System;
using Octopus.Shared.Orchestration.Logging;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Activities
{
    public class PrefixedActivityLogDecorator : ActivityLogDecorator
    {
        readonly string prefix;

        public PrefixedActivityLogDecorator(string prefix, ITrace inner) : base(inner)
        {
            this.prefix = prefix;
        }

        protected override void WriteEvent(TraceCategory category, Exception error, string messageText)
        {
            base.WriteEvent(category, error, (prefix + messageText));
        }
    }
}
