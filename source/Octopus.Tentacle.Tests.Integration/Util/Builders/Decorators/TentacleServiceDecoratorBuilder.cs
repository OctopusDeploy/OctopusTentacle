using System;
using System.Collections.Generic;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.SyncAndAsyncProxies;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public delegate T Decorator<T>(T service);

    public class TentacleServiceDecoratorBuilder
    {
        private readonly List<Decorator<IAsyncClientScriptService>> scriptServiceDecorators = new();
        private readonly List<Decorator<IAsyncClientScriptServiceV2>> scriptServiceV2Decorators = new();
        private readonly List<Decorator<IAsyncClientScriptServiceV3Alpha>> scriptServiceV3AlphaDecorators = new();
        private readonly List<Decorator<IAsyncClientFileTransferService>> fileTransferServiceDecorators = new();
        private readonly List<Decorator<IAsyncClientCapabilitiesServiceV2>> capabilitiesServiceV2Decorators = new();

        public TentacleServiceDecoratorBuilder DecorateScriptServiceWith(Decorator<IAsyncClientScriptService> scriptServiceDecorator)
        {
            this.scriptServiceDecorators.Add(scriptServiceDecorator);
            return this;
        }

        public TentacleServiceDecoratorBuilder DecorateScriptServiceV2With(Decorator<IAsyncClientScriptServiceV2> scriptServiceV2Decorator)
        {
            this.scriptServiceV2Decorators.Add(scriptServiceV2Decorator);
            return this;
        }

        public TentacleServiceDecoratorBuilder DecorateScriptServiceV3AlphaWith(Decorator<IAsyncClientScriptServiceV3Alpha> scriptServiceV3AlphaDecorator)
        {
            this.scriptServiceV3AlphaDecorators.Add(scriptServiceV3AlphaDecorator);
            return this;
        }

        public TentacleServiceDecoratorBuilder DecorateScriptServiceV2With(Action<ScriptServiceV2DecoratorBuilder> scriptServiceV2Decorator)
        {
            var b = new ScriptServiceV2DecoratorBuilder();
            scriptServiceV2Decorator(b);
            this.DecorateScriptServiceV2With(b.Build());
            return this;
        }

        public TentacleServiceDecoratorBuilder DecorateFileTransferServiceWith(Decorator<IAsyncClientFileTransferService> fileTransferServiceDecorator)
        {
            this.fileTransferServiceDecorators.Add(fileTransferServiceDecorator);
            return this;
        }

        public TentacleServiceDecoratorBuilder DecorateFileTransferServiceWith(Action<FileTransferServiceDecoratorBuilder> fileTransferServiceDecorator)
        {
            var b = new FileTransferServiceDecoratorBuilder();
            fileTransferServiceDecorator(b);
            this.DecorateFileTransferServiceWith(b.Build());
            return this;
        }

        public TentacleServiceDecoratorBuilder DecorateCapabilitiesServiceV2With(Decorator<IAsyncClientCapabilitiesServiceV2> capabilitiesServiceV2Decorator)
        {
            this.capabilitiesServiceV2Decorators.Add(capabilitiesServiceV2Decorator);
            return this;
        }

        public TentacleServiceDecoratorBuilder DecorateCapabilitiesServiceV2With(Action<CapabilitiesServiceV2DecoratorBuilder> capabilitiesServiceDecorator)
        {
            var b = new CapabilitiesServiceV2DecoratorBuilder();
            capabilitiesServiceDecorator(b);
            this.DecorateCapabilitiesServiceV2With(b.Build());
            return this;
        }

        internal ITentacleServiceDecoratorFactory Build()
        {
            return new FooTentacleServiceDecoratorFactory(
                Combine(scriptServiceDecorators),
                Combine(scriptServiceV2Decorators),
                Combine(scriptServiceV3AlphaDecorators),
                Combine(fileTransferServiceDecorators),
                Combine(capabilitiesServiceV2Decorators));
        }

        public static Decorator<T> Combine<T>(List<Decorator<T>> chain) where T : class
        {
            return t =>
            {
                var reverseChain = new List<Decorator<T>>(chain);
                reverseChain.Reverse();

                foreach (var func in reverseChain)
                {
                    t = func(t);
                }

                return t;
            };
        }

        private class FooTentacleServiceDecoratorFactory : AsyncToSyncTentacleServiceDecorator, ITentacleServiceDecoratorFactory
        {
            readonly Decorator<IAsyncClientScriptServiceV3Alpha> scriptServiceV3AlphaDecorator;

            public FooTentacleServiceDecoratorFactory(
                Decorator<IAsyncClientScriptService> scriptServiceDecorator,
                Decorator<IAsyncClientScriptServiceV2> scriptServiceV2Decorator,
                Decorator<IAsyncClientScriptServiceV3Alpha> scriptServiceV3AlphaDecorator,
                Decorator<IAsyncClientFileTransferService> fileTransferServiceDecorator,
                Decorator<IAsyncClientCapabilitiesServiceV2> capabilitiesServiceV2Decorator) :
                base(scriptServiceDecorator, scriptServiceV2Decorator, fileTransferServiceDecorator, capabilitiesServiceV2Decorator)
            {
                this.scriptServiceV3AlphaDecorator = scriptServiceV3AlphaDecorator;
            }

            public IAsyncClientScriptService Decorate(IAsyncClientScriptService scriptService)
            {
                return scriptServiceDecorator(scriptService);
            }

            public IAsyncClientScriptServiceV2 Decorate(IAsyncClientScriptServiceV2 scriptService)
            {
                return scriptServiceV2Decorator(scriptService);
            }

            public IAsyncClientFileTransferService Decorate(IAsyncClientFileTransferService service)
            {
                return fileTransferServiceDecorator(service);
            }

            public IAsyncClientCapabilitiesServiceV2 Decorate(IAsyncClientCapabilitiesServiceV2 service)
            {
                return capabilitiesServiceV2Decorator(service);
            }

            public IAsyncClientScriptServiceV3Alpha Decorate(IAsyncClientScriptServiceV3Alpha service)
            {
                return scriptServiceV3AlphaDecorator(service);
            }
        }
    }

    internal abstract class AsyncToSyncTentacleServiceDecorator
    {
        protected readonly Decorator<IAsyncClientScriptService> scriptServiceDecorator;
        protected readonly Decorator<IAsyncClientScriptServiceV2> scriptServiceV2Decorator;
        protected readonly Decorator<IAsyncClientFileTransferService> fileTransferServiceDecorator;
        protected readonly Decorator<IAsyncClientCapabilitiesServiceV2> capabilitiesServiceV2Decorator;

        protected AsyncToSyncTentacleServiceDecorator(Decorator<IAsyncClientScriptService> scriptServiceDecorator,
            Decorator<IAsyncClientScriptServiceV2> scriptServiceV2Decorator,
            Decorator<IAsyncClientFileTransferService> fileTransferServiceDecorator,
            Decorator<IAsyncClientCapabilitiesServiceV2> capabilitiesServiceV2Decorator)
        {
            this.scriptServiceDecorator = scriptServiceDecorator;
            this.scriptServiceV2Decorator = scriptServiceV2Decorator;
            this.fileTransferServiceDecorator = fileTransferServiceDecorator;
            this.capabilitiesServiceV2Decorator = capabilitiesServiceV2Decorator;
        }

        public IClientScriptService Decorate(IClientScriptService scriptService)
        {
            return AsyncToSyncProxy.ProxyAsyncToSync(scriptServiceDecorator(AsyncToSyncProxy.ProxySyncToAsync(scriptService)));
        }

        public IClientScriptServiceV2 Decorate(IClientScriptServiceV2 scriptService)
        {
            return AsyncToSyncProxy.ProxyAsyncToSync(scriptServiceV2Decorator(AsyncToSyncProxy.ProxySyncToAsync(scriptService)));
        }

        public IClientFileTransferService Decorate(IClientFileTransferService service)
        {
            return AsyncToSyncProxy.ProxyAsyncToSync(fileTransferServiceDecorator(AsyncToSyncProxy.ProxySyncToAsync(service)));
        }

        public IClientCapabilitiesServiceV2 Decorate(IClientCapabilitiesServiceV2 service)
        {
            return AsyncToSyncProxy.ProxyAsyncToSync(capabilitiesServiceV2Decorator(AsyncToSyncProxy.ProxySyncToAsync(service)));
        }
    }

    public static class TentacleServiceDecoratorBuilderExtensionMethods
    {
        public static TentacleServiceDecoratorBuilder LogAllCalls(this TentacleServiceDecoratorBuilder builder)
        {
            return builder.LogCallsToCapabilitiesServiceV2()
                .LogCallsToScriptService()
                .LogCallsToScriptServiceV2()
                .LogCallsToFileTransferService();
        }

        public static TentacleServiceDecoratorBuilder CountAllCalls(this TentacleServiceDecoratorBuilder builder,
            out CapabilitiesServiceV2CallCounts capabilitiesServiceV2CallCounts,
            out ScriptServiceCallCounts scriptServiceCallCounts,
            out ScriptServiceV2CallCounts scriptServiceV2CallCounts,
            out ScriptServiceV3AlphaCallCounts scriptServiceV3AlphaCallCounts,
            out FileTransferServiceCallCounts fileTransferServiceServiceCallCounts)
        {
            builder.CountCallsToCapabilitiesServiceV2(out var capabilitiesServiceCallCountsOut)
                .CountCallsToScriptService(out var scriptServiceCallCountsOut)
                .CountCallsToScriptServiceV2(out var scriptServiceV2CallCountsOut)
                .CountCallsToScriptServiceV3Alpha(out var scriptServiceV3AlphaCallCountsOut)
                .CountCallsToFileTransferService(out var fileTransferServiceCallCountsOut);

            capabilitiesServiceV2CallCounts = capabilitiesServiceCallCountsOut;
            scriptServiceCallCounts = scriptServiceCallCountsOut;
            scriptServiceV2CallCounts = scriptServiceV2CallCountsOut;
            scriptServiceV3AlphaCallCounts = scriptServiceV3AlphaCallCountsOut;
            fileTransferServiceServiceCallCounts = fileTransferServiceCallCountsOut;

            return builder;
        }

        public static TentacleServiceDecoratorBuilder LogAndCountAllCalls(this TentacleServiceDecoratorBuilder builder,
            out CapabilitiesServiceV2CallCounts capabilitiesServiceV2CallCounts,
            out ScriptServiceCallCounts scriptServiceCallCounts,
            out ScriptServiceV2CallCounts scriptServiceV2CallCounts,
            out ScriptServiceV3AlphaCallCounts scriptServiceV3AlphaCallCounts,
            out FileTransferServiceCallCounts fileTransferServiceServiceCallCounts)
        {
            builder.LogAllCalls()
                .CountAllCalls(out var capabilitiesServiceCallCountsOut,
                    out var scriptServiceCallCountsOut,
                    out var scriptServiceV2CallCountsOut,
                    out var scriptServiceV3AlphaCountsOut,
                    out var fileTransferServiceCallCountsOut);

            capabilitiesServiceV2CallCounts = capabilitiesServiceCallCountsOut;
            scriptServiceCallCounts = scriptServiceCallCountsOut;
            scriptServiceV2CallCounts = scriptServiceV2CallCountsOut;
            scriptServiceV3AlphaCallCounts = scriptServiceV3AlphaCountsOut;
            fileTransferServiceServiceCallCounts = fileTransferServiceCallCountsOut;

            return builder;
        }

        public static TentacleServiceDecoratorBuilder RecordAllExceptions(this TentacleServiceDecoratorBuilder builder,
            out CapabilitiesServiceV2Exceptions capabilitiesServiceV2Exceptions,
            out ScriptServiceExceptions scriptServiceExceptions,
            out ScriptServiceV2Exceptions scriptServiceV2Exceptions,
            out FileTransferServiceExceptions fileTransferServiceServiceExceptions)
        {
            builder.RecordExceptionThrownInCapabilitiesServiceV2(out var capabilitiesServiceV2ExceptionOut)
                .RecordExceptionThrownInScriptService(out var scriptServiceExceptionsOut)
                .RecordExceptionThrownInScriptServiceV2(out var scriptServiceV2ExceptionsOut)
                .RecordExceptionThrownInFileTransferService(out var fileTransferServiceExceptionsOut);

            capabilitiesServiceV2Exceptions = capabilitiesServiceV2ExceptionOut;
            scriptServiceExceptions = scriptServiceExceptionsOut;
            scriptServiceV2Exceptions = scriptServiceV2ExceptionsOut;
            fileTransferServiceServiceExceptions = fileTransferServiceExceptionsOut;

            return builder;
        }
    }
}