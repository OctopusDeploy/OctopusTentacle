using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Specialized;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public static class ExceptionContractAssertionBuilderExtensionMethods
    {
        public static async Task<ExceptionAssertions<Exception>> ThrowExceptionContractAsync(
            this NonGenericAsyncFunctionAssertions should,
            ExceptionContract expected,
            string because = "",
            params object[] becauseArgs)
        {
            var exceptionAssertions = await should.ThrowAsync<Exception>(because, becauseArgs);
            var exception = exceptionAssertions.And;

            exception.ShouldMatchExceptionContract(expected, because, becauseArgs);

            return exceptionAssertions;
        }

        public static async Task<ExceptionAssertions<Exception>> ThrowExceptionContractAsync<T>(
            this GenericAsyncFunctionAssertions<T> should,
            ExceptionContract expected,
            string because = "",
            params object[] becauseArgs)
        {
            var exceptionAssertions = await should.ThrowAsync<Exception>(because, becauseArgs);
            var exception = exceptionAssertions.And;

            exception.ShouldMatchExceptionContract(expected, because, becauseArgs);

            return exceptionAssertions;
        }

        public static void ShouldMatchExceptionContract(
            this Exception exception,
            ExceptionContract expected,
            string because = "",
            params object[] becauseArgs)
        {
            exception.Should().NotBeNull(because, becauseArgs);

            using var scope = new AssertionScope();
            
            Execute.Assertion
                .ForCondition(expected.ExceptionTypes.Any(v => v == exception.GetType()))
                .BecauseOf(because, becauseArgs)
                .FailWith("Expected {context} to be {0}{reason}, but found {1}.", expected.ExceptionTypes.Select(x => x.Name), exception.GetType().Name);
        
            exception.Message.Should().ContainAny(expected.ExceptionMessageShouldContainAny, because, becauseArgs);
        }
    }
}
