using System;
using System.Diagnostics;
using NUnit.Framework;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class IntegrationTestTimeout : TimeoutAttribute
    {
        public IntegrationTestTimeout(int timeoutInSeconds) : base((int)TimeSpan.FromSeconds(timeoutInSeconds).TotalMilliseconds)
        {
        }

        public IntegrationTestTimeout() : base(TestTimeoutInMilliseconds())
        {
        }

        public static int TestTimeoutInMilliseconds()
        {
            if (Debugger.IsAttached)
            {
                return (int)TimeSpan.FromHours(1).TotalMilliseconds;
            }

            var windows2012Timeout = GetWindows2012Timeout();
            if (windows2012Timeout is not null)
            {
                return windows2012Timeout.Value;
            }

            return (int)TimeSpan.FromMinutes(2).TotalMilliseconds;
        }

        static int? GetWindows2012Timeout()
        {
            if (int.TryParse(Environment.GetEnvironmentVariable("Windows2012_IntegrationTests_Timeout_Minute"), out var windows2012Timeout))
            {
                return (int)TimeSpan.FromMinutes(windows2012Timeout).TotalMilliseconds;
            }

            return null;
        }
    }
}