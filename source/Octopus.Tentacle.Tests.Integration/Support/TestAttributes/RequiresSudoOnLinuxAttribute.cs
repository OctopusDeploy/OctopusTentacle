using System;
using NUnit.Framework;

namespace Octopus.Tentacle.Tests.Integration.Support.TestAttributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class RequiresSudoOnLinuxAttribute : CategoryAttribute
    { }
}
