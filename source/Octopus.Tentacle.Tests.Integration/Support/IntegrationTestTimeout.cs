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

            return GetTimeoutFromEnvironmentVariable() ?? (int)TimeSpan.FromMinutes(2).TotalMilliseconds;
        }
        
        static int? GetTimeoutFromEnvironmentVariable()
        {
            if (int.TryParse(Environment.GetEnvironmentVariable("IntegrationTest_Timeout_Minutes"), out var timeoutFromEnv))
            {
                return (int)TimeSpan.FromMinutes(timeoutFromEnv).TotalMilliseconds;
            }

            return null;
        }
    }
}