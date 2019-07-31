using System;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Configuration.Proxy
{
    public interface ISensitiveValueMasker
    {
        string MaskSensitiveValues(string rawMessage);
    }

    //Wrap the log context in this until we extract the the masking from it
    public class SensitiveValueMasker : ISensitiveValueMasker
    {
        readonly ILogContext logContext;

        public SensitiveValueMasker(ILogContext logContext)
        {
            this.logContext = logContext;
        }

        public string MaskSensitiveValues(string rawMessage)
        {
            string maskedMessage = null;
            logContext.SafeSanitize(rawMessage, s => maskedMessage = s);
            logContext.Flush();

            return maskedMessage;
        }
    }
}