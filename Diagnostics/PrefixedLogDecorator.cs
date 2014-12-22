using System;

namespace Octopus.Shared.Diagnostics
{
    public class PrefixedLogDecorator : AbstractLog
    {
        readonly ILog inner;
        readonly string prefix;

        public PrefixedLogDecorator(string prefix, ILog inner)
        {
            this.prefix = prefix;
            this.inner = inner;
        }

        protected override void WriteEvent(TraceCategory category, Exception error, string messageText)
        {
            inner.Write(category, error, prefix + messageText);
        }

        public override ILog BeginOperation(string messageText)
        {
            return inner.BeginOperation(prefix + messageText);
        }

        public override void EndOperation()
        {
            inner.EndOperation();
        }

        public override void UpdateProgress(int progressPercentage, string messageText)
        {
            inner.UpdateProgress(progressPercentage, prefix + messageText);
        }

        public override bool IsEnabled(TraceCategory category)
        {
            return inner.IsEnabled(category);
        }
    }
}
