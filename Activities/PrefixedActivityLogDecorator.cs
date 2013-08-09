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

        public override void Write(TraceCategory level, object message)
        {
            base.Write(level, (object)(prefix + message));
        }
    }
}