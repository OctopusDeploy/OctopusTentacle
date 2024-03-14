using NSubstitute;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Client.Tests.Builders
{
    public class AsyncClientScriptServiceV3AlphaBuilder
    {
        public IAsyncClientScriptServiceV3Alpha Build()
        {
            return Substitute.For<IAsyncClientScriptServiceV3Alpha>();
        }

        public static IAsyncClientScriptServiceV3Alpha Default() => new AsyncClientScriptServiceV3AlphaBuilder().Build();
    }
}