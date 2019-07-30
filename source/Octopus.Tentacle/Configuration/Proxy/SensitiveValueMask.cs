using System;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Configuration.Proxy
{
    public interface ISensitiveValueMask
    {
        void SafeSanitizeAndFlush(string raw, Action<string> action);
    }

    //Wrap the log context in this until we extract the the masking from it
    public class SensitiveValueMask : ISensitiveValueMask
    {
        readonly ILogContext logContext;

        public SensitiveValueMask(ILogContext logContext)
        {
            this.logContext = logContext;
        }

        public void SafeSanitizeAndFlush(string raw, Action<string> action)
        {
            logContext.SafeSanitize(raw, action);
            logContext.Flush();
        }
    }
}