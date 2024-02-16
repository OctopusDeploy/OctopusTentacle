using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Tentacle.Util;

namespace Octopus.Manager.Tentacle.Tests.Utils
{
    public static class CommandLineInvocationExtension
    {
        public static string ToCommandLineString(this IEnumerable<CommandLineInvocation> source)
        {
            return string.Join(Environment.NewLine, source.Select(z => z.ToString()));
        }
    }
}