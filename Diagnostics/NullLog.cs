using System;

namespace Octopus.Platform.Diagnostics
{
    public class NullLog : AbstractLog
    {
        protected override void WriteEvent(TraceCategory category, Exception error, string messageText)
        {
            
        }

        public override ILog BeginOperation(string messageText)
        {
            return this;
        }

        public override void EndOperation()
        {            
        }

        public override void UpdateProgress(int progressPercentage, string messageText)
        {
        }

        public override bool IsEnabled(TraceCategory category)
        {
            return false;
        }
    }
}