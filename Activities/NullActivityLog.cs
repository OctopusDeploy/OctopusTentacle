using System;
using Octopus.Shared.Diagnostics;
using log4net;
using log4net.Core;

namespace Octopus.Shared.Activities
{
    public class NullActivityLog : AbstractActivityLog
    {
        readonly ILog log;

        public NullActivityLog() : this(null)
        {
        }

        public NullActivityLog(ILog log)
        {
            this.log = log ?? Logger.Default;
        }

        public override void Write(Level level, object message)
        {
            log.Logger.Log(typeof(NullActivityLog), level, message, null);
        }

        public override IActivityLog OverwritePrevious()
        {
            return this;
        }

        public override string GetLog()
        {
            return string.Empty;
        }
    }
}