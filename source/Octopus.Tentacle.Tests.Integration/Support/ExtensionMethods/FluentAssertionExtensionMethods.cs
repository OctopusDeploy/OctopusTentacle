using System;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Primitives;

namespace Octopus.Tentacle.Tests.Integration.Support.ExtensionMethods
{
    public static class FluentAssertionExtensionMethods
    {
        public static AndConstraint<ObjectAssertions> BeTaskOrOperationCancelledException(this ObjectAssertions should)
        {
            return should.Match(x => x is TaskCanceledException || x is OperationCanceledException);
        }
    }
}
