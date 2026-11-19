using System;
using Halibut;
using Halibut.Diagnostics;
using Halibut.Diagnostics.LogCreators;
using Halibut.Logging;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Contracts.Observability;
using Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators;
using Octopus.Tentacle.Tests.Integration.Common.Logging;

namespace Octopus.Tentacle.Tests.Integration.Common;

/// <summary>
/// Builds the server side of Halibut, plus TentacleClients that talk to a Tentacle from it, for tests that stand in
/// for Octopus Server.
/// </summary>
public static class TestServerHalibut
{
    public static HalibutRuntime BuildServerHalibutRuntime()
    {
        return new HalibutRuntimeBuilder()
            .WithServerCertificate(TestCertificates.Server)
            .WithHalibutTimeoutsAndLimits(HalibutTimeoutsAndLimits.RecommendedValues())
            .WithLogFactory(new TestContextLogCreator("Server", LogLevel.Trace).ToCachingLogFactory())
            .Build();
    }

    public static TentacleClient BuildTentacleClient(Uri uri, string? thumbprint, HalibutRuntime halibutRuntime, Action<TentacleServiceDecoratorBuilder> tentacleServiceDecoratorBuilderAction)
    {
        var endpoint = new ServiceEndPoint(uri, thumbprint, halibutRuntime.TimeoutsAndLimits);

        var retrySettings = new RpcRetrySettings(true, TimeSpan.FromMinutes(2));
        var clientOptions = new TentacleClientOptions(retrySettings);

        TentacleClient.CacheServiceWasNotFoundResponseMessages(halibutRuntime);

        var builder = new TentacleServiceDecoratorBuilder();
        tentacleServiceDecoratorBuilderAction(builder);

        return new TentacleClient(
            endpoint,
            halibutRuntime,
            new PollingTentacleScriptObserverBackoffStrategy(),
            new NoTentacleClientObserver(),
            clientOptions,
            builder.Build());
    }
}
