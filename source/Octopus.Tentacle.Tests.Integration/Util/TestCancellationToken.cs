using System;
using System.Threading;

namespace Octopus.Tentacle.Tests.Integration.Util
{
    public class TestCancellationToken
    {
        public static CancellationToken Token()
        {
            return new CancellationTokenSource(TimeSpan.FromMinutes(4)).Token;
        }
    }
}