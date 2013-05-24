using System;

namespace Octopus.Shared.Activities
{
    public class PrefixedActivityLogDecorator : ActivityLogDecorator
    {
        readonly string prefix;

        public PrefixedActivityLogDecorator(string prefix, IActivityLog inner) : base(inner)
        {
            this.prefix = prefix;
        }

        public override void Write(ActivityLogLevel level, object message)
        {
            base.Write(level, prefix + message);
        }
    }
}