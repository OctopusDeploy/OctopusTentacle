using NSubstitute;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Client.Tests.Builders
{
    public class AsyncClientScriptServiceBuilder
    {
        public IAsyncClientScriptService Build()
        {
            return Substitute.For<IAsyncClientScriptService>();
        }

        public static IAsyncClientScriptService Default() => new AsyncClientScriptServiceBuilder().Build();
    }
}