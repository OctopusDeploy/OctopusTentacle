using System;
using Octopus.Platform.Diagnostics;
using Octopus.Shared.Activities;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Orchestration.Logging
{
    public class ChildActivityLog : AbstractLog
    {
        readonly IActivity activity;
        readonly Lazy<LoggerReference> child;

        public ChildActivityLog(string messageText, IActivity activity, LoggerReference parent)
        {
            this.activity = activity;
            child = new Lazy<LoggerReference>(() => activity.CreateChild(parent, messageText), isThreadSafe: true);
        }

        protected override void WriteEvent(TraceCategory category, Exception error, string messageText)
        {
            activity.Write(child.Value, category, error, messageText);
        }

        public override ILog BeginOperation(string messageText)
        {
            return new ChildActivityLog(messageText, activity, child.Value);
        }

        public override void UpdateProgress(int progressPercentage, string messageText)
        {
            activity.UpdateProgress(child.Value, progressPercentage, messageText);
        }

        public override bool IsEnabled(TraceCategory category)
        {
            return activity.IsEnabled(category);
        }
    }
}
