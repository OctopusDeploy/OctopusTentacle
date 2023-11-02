using System;
using System.Collections.Generic;
using System.Linq;
using Castle.DynamicProxy;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Interceptors;

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

        readonly Dictionary<Type, List<IInterceptor>> interceptors = new();

        public TentacleServiceDecoratorBuilder RegisterInterceptor<TService>(IInterceptor interceptor)
            => RegisterInterceptor(typeof(TService), interceptor);

        public TentacleServiceDecoratorBuilder RegisterInterceptor(Type serviceType, IInterceptor interceptor)
        {
            if (interceptors.TryGetValue(serviceType, out var list))
            {
                list.Add(interceptor);
            }
            else
            {
                interceptors.Add(serviceType, new List<IInterceptor>
                {
                    interceptor
                });
            }

            return this;
        }

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
            return new DynamicProxyTentacleServiceDecoratorFactory(interceptors);
        }

        class DynamicProxyTentacleServiceDecoratorFactory : ITentacleServiceDecoratorFactory
        {
            static readonly ProxyGenerator Generator = new();
            readonly Dictionary<Type, List<IInterceptor>> serviceInterceptors;

            static List<IInterceptor> StandardInterceptors = new()
            {
                //all calls should be logged
                new CallLoggingInterceptor().ToInterceptor()
            };

            public DynamicProxyTentacleServiceDecoratorFactory(Dictionary<Type, List<IInterceptor>> serviceInterceptors)
            {
                this.serviceInterceptors = serviceInterceptors;
            }

            public IClientScriptService Decorate(IClientScriptService scriptService) => GetProxy(scriptService);

            public IAsyncClientScriptService Decorate(IAsyncClientScriptService scriptService) => GetProxy(scriptService);

            public IClientScriptServiceV2 Decorate(IClientScriptServiceV2 scriptService) => GetProxy(scriptService);

            public IAsyncClientScriptServiceV2 Decorate(IAsyncClientScriptServiceV2 scriptService) => GetProxy(scriptService);

            public IClientFileTransferService Decorate(IClientFileTransferService service) => GetProxy(service);

            public IAsyncClientFileTransferService Decorate(IAsyncClientFileTransferService service) => GetProxy(service);

            public IClientCapabilitiesServiceV2 Decorate(IClientCapabilitiesServiceV2 service) => GetProxy(service);

            public IAsyncClientCapabilitiesServiceV2 Decorate(IAsyncClientCapabilitiesServiceV2 service) => GetProxy(service);

            public IAsyncClientScriptServiceV3Alpha Decorate(IAsyncClientScriptServiceV3Alpha service) => GetProxy(service);

            T GetProxy<T>(T scriptService) where T : class
            {
                var interceptors = serviceInterceptors.TryGetValue(typeof(T), out var svcInterceptors)
                    ? svcInterceptors.ToArray()
                    : Array.Empty<IInterceptor>();

                interceptors = StandardInterceptors.Concat(interceptors).ToArray();
                return Generator.CreateInterfaceProxyWithTargetInterface(scriptService, interceptors);
            }
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