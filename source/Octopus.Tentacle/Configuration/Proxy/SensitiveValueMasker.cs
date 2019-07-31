using System;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Configuration.Proxy
{
    public interface ISensitiveValueMasker
    {
        void SafeSanitizeAndFlush(string raw, Action<string> action);
    }

    //Wrap the log context in this until we extract the the masking from it
    public class SensitiveValueMasker : ISensitiveValueMasker
    {
        readonly ILogContext logContext;

        public SensitiveValueMasker(ILogContext logContext)
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