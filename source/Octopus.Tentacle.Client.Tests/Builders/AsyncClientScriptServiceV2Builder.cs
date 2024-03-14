using NSubstitute;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Client.Tests.Builders
{
    public class AsyncClientScriptServiceV2Builder
    {
        public IAsyncClientScriptServiceV2 Build()
        {
            return Substitute.For<IAsyncClientScriptServiceV2>();
        }

        public static IAsyncClientScriptServiceV2 Default() => new AsyncClientScriptServiceV2Builder().Build();
    }
}