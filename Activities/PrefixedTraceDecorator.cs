using System;
using Octopus.Shared.Orchestration.Logging;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Activities
{
    public class PrefixedTraceDecorator : AbstractTrace
    {
        readonly ITrace inner;
        readonly string prefix;

        public PrefixedTraceDecorator(string prefix, ITrace inner)
        {
            this.prefix = prefix;
            this.inner = inner;
        }

        protected override void WriteEvent(TraceCategory category, Exception error, string messageText)
        {
            inner.Write(category, error, prefix + messageText);
        }

        public override ITrace BeginOperation(string messageText)
        {
            return inner.BeginOperation(prefix + messageText);
        }

        public override void UpdateProgress(int progressPercentage, string messageText)
        {
            inner.UpdateProgress(progressPercentage, prefix + messageText);
        }
    }
}
