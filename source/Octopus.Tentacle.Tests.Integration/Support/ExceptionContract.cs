using System;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class ExceptionContract
    {
        public Type[] ExceptionTypes { get; }
        public string[] ExceptionMessageShouldContainAny { get; }

        public ExceptionContract(Type exceptionTypes, string[] exceptionMessageShouldContainAny)
        {
            ExceptionTypes = new[] { exceptionTypes };
            ExceptionMessageShouldContainAny = exceptionMessageShouldContainAny;
        }

        public ExceptionContract(Type[] exceptionTypes, string[] exceptionMessageShouldContainAny)
        {
            ExceptionTypes = exceptionTypes;
            ExceptionMessageShouldContainAny = exceptionMessageShouldContainAny;
        }
    }
}