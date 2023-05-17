using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Tests.Integration.Util
{
    public static class ListProcessOutputExtensionMethods
    {
        public static string JoinLogs(this List<ProcessOutput> logs)
        {
            return String.Join("\n", logs.Select(l => l.Text).ToArray());
        }
    }
}