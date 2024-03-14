using System;
using System.Threading.Tasks;
using NSubstitute;
using Octopus.Diagnostics;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Client.Tests.Builders
{
    class ScriptOrchestratorFactoryBuilder
    {
        IAsyncClientScriptService? clientScriptServiceV1;
        IAsyncClientScriptServiceV2? clientScriptServiceV2;
        IAsyncClientScriptServiceV3Alpha? clientScriptServiceV3Alpha;
        IAsyncClientCapabilitiesServiceV2? clientCapabilitiesServiceV2;
        IScriptObserverBackoffStrategy? scriptObserverBackoffStrategy;
        RpcCallExecutor? rpcCallExecutor;
        ClientOperationMetricsBuilder? clientOperationMetricsBuilder;
        OnScriptStatusResponseReceived? onScriptStatusResponseReceived;
        OnScriptCompleted? onScriptCompleted;
        TimeSpan? onCancellationAbandonCompleteScriptAfter;
        TentacleClientOptions? clientOptions;
        ILog? logger;

        public ScriptOrchestratorFactoryBuilder WithClientScriptServiceV1(IAsyncClientScriptService clientScriptServiceV1)
        {
            this.clientScriptServiceV1 = clientScriptServiceV1;
            return this;
        }

        public ScriptOrchestratorFactoryBuilder WithClientScriptServiceV2(Func<AsyncClientScriptServiceBuilder, AsyncClientScriptServiceBuilder> builder)
        {
            clientScriptServiceV1 = builder(new AsyncClientScriptServiceBuilder()).Build();
            return this;
        }

        public ScriptOrchestratorFactoryBuilder WithClientScriptServiceV2(IAsyncClientScriptServiceV2 clientScriptServiceV2)
        {
            this.clientScriptServiceV2 = clientScriptServiceV2;
            return this;
        }

        public ScriptOrchestratorFactoryBuilder WithClientScriptServiceV2(Func<AsyncClientScriptServiceV2Builder, AsyncClientScriptServiceV2Builder> builder)
        {
            clientScriptServiceV2 = builder(new AsyncClientScriptServiceV2Builder()).Build();
            return this;
        }

        public ScriptOrchestratorFactoryBuilder WithClientScriptServiceV3Alpha(IAsyncClientScriptServiceV3Alpha clientScriptServiceV3Alpha)
        {
            this.clientScriptServiceV3Alpha = clientScriptServiceV3Alpha;
            return this;
        }

        public ScriptOrchestratorFactoryBuilder WithClientScriptServiceV3Alpha(Func<AsyncClientScriptServiceV3AlphaBuilder, AsyncClientScriptServiceV3AlphaBuilder> builder)
        {
            clientScriptServiceV3Alpha = builder(new AsyncClientScriptServiceV3AlphaBuilder()).Build();
            return this;
        }

        public ScriptOrchestratorFactoryBuilder WithClientCapabilitiesServiceV2(IAsyncClientCapabilitiesServiceV2 clientCapabilitiesServiceV2)
        {
            this.clientCapabilitiesServiceV2 = clientCapabilitiesServiceV2;
            return this;
        }

        public ScriptOrchestratorFactoryBuilder WithClientCapabilitiesServiceV2(Func<AsyncClientCapabilitiesServiceV2Builder, AsyncClientCapabilitiesServiceV2Builder> builder)
        {
            clientCapabilitiesServiceV2 = builder(new AsyncClientCapabilitiesServiceV2Builder()).Build();
            return this;
        }

        public ScriptOrchestratorFactoryBuilder WithScriptObserverBackoffStrategy(IScriptObserverBackoffStrategy scriptObserverBackoffStrategy)
        {
            this.scriptObserverBackoffStrategy = scriptObserverBackoffStrategy;
            return this;
        }

        public ScriptOrchestratorFactoryBuilder WithScriptObserverBackoffStrategy(Func<ScriptObserverBackoffStrategyBuilder, ScriptObserverBackoffStrategyBuilder> builder)
        {
            scriptObserverBackoffStrategy = builder(new ScriptObserverBackoffStrategyBuilder()).Build();
            return this;
        }

        public ScriptOrchestratorFactoryBuilder WithRpcCallExecutor(RpcCallExecutor rpcCallExecutor)
        {
            this.rpcCallExecutor = rpcCallExecutor;
            return this;
        }

        public ScriptOrchestratorFactoryBuilder WithRpcCallExecutor(Func<RpcCallExecutorBuilder, RpcCallExecutorBuilder> builder)
        {
            rpcCallExecutor = builder(new RpcCallExecutorBuilder()).Build();
            return this;
        }

        public ScriptOrchestratorFactoryBuilder WithClientOperationMetricsBuilder(ClientOperationMetricsBuilder clientOperationMetricsBuilder)
        {
            this.clientOperationMetricsBuilder = clientOperationMetricsBuilder;
            return this;
        }

        public ScriptOrchestratorFactoryBuilder WithClientOperationMetricsBuilder(Func<ClientOperationMetricsBuilderBuilder, ClientOperationMetricsBuilderBuilder> builder)
        {
            clientOperationMetricsBuilder = builder(new ClientOperationMetricsBuilderBuilder()).Build();
            return this;
        }

        public ScriptOrchestratorFactoryBuilder WithOnScriptStatusResponseReceived(OnScriptStatusResponseReceived onScriptStatusResponseReceived)
        {
            this.onScriptStatusResponseReceived = onScriptStatusResponseReceived;
            return this;
        }

        public ScriptOrchestratorFactoryBuilder WithOnScriptCompleted(OnScriptCompleted onScriptCompleted)
        {
            this.onScriptCompleted = onScriptCompleted;
            return this;
        }

        public ScriptOrchestratorFactoryBuilder WithOnCancellationAbandonCompleteScriptAfter(TimeSpan onCancellationAbandonCompleteScriptAfter)
        {
            this.onCancellationAbandonCompleteScriptAfter = onCancellationAbandonCompleteScriptAfter;
            return this;
        }

        public ScriptOrchestratorFactoryBuilder WithClientOptions(TentacleClientOptions clientOptions)
        {
            this.clientOptions = clientOptions;
            return this;
        }

        public ScriptOrchestratorFactoryBuilder WithClientOptions(Func<TentacleClientOptionsBuilder, TentacleClientOptionsBuilder> builder)
        {
            clientOptions = builder(new TentacleClientOptionsBuilder()).Build();
            return this;
        }

        public ScriptOrchestratorFactoryBuilder WithLogger(ILog logger)
        {
            this.logger = logger;
            return this;
        }

        public ScriptOrchestratorFactory Build() =>
            new(
                clientScriptServiceV1 ?? AsyncClientScriptServiceBuilder.Default(),
                clientScriptServiceV2 ?? AsyncClientScriptServiceV2Builder.Default(),
                clientScriptServiceV3Alpha ?? AsyncClientScriptServiceV3AlphaBuilder.Default(),
                clientCapabilitiesServiceV2 ?? AsyncClientCapabilitiesServiceV2Builder.Default(),
                scriptObserverBackoffStrategy ?? ScriptObserverBackoffStrategyBuilder.Default(),
                rpcCallExecutor ?? RpcCallExecutorBuilder.Default(),
                clientOperationMetricsBuilder ?? ClientOperationMetricsBuilderBuilder.Default(),
                onScriptStatusResponseReceived ?? (_ =>
                {
                }),
                onScriptCompleted ?? (_ => Task.CompletedTask),
                onCancellationAbandonCompleteScriptAfter ?? TimeSpan.FromSeconds(5),
                clientOptions ?? TentacleClientOptionsBuilder.Default(),
                logger ?? Substitute.For<ILog>()
            );

        public static ScriptOrchestratorFactory Default() => new ScriptOrchestratorFactoryBuilder().Build();
    }
}