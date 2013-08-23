using System;
using Octopus.Shared.Activities;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Orchestration.Logging
{
    public class ChildActivityTrace : AbstractTrace
    {
        readonly IActivity activity;
        readonly Lazy<LoggerReference> child;

        public ChildActivityTrace(string messageText, IActivity activity, LoggerReference parent)
        {
            this.activity = activity;
            child = new Lazy<LoggerReference>(() => activity.CreateChild(parent, messageText), isThreadSafe: true);
        }

        protected override void WriteEvent(TraceCategory category, Exception error, string messageText)
        {
            activity.Write(child.Value, category, error, messageText);
        }

        public override ITrace BeginOperation(string messageText)
        {
            return new ChildActivityTrace(messageText, activity, child.Value);
        }

        public override void UpdateProgress(int progressPercentage, string messageText)
        {
            activity.UpdateProgress(child.Value, progressPercentage, messageText);
        }
    }
}
