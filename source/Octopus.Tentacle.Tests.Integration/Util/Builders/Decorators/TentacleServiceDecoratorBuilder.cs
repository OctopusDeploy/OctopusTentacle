using System;
using System.Collections.Generic;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.Proxies;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public delegate T Decorator<T>(T service);

    public delegate object ProxyDecoratorFactory(object service);

    public class TentacleServiceDecoratorBuilder
    {
        readonly Dictionary<Type, List<ProxyDecoratorFactory>> registeredProxyDecorators = new();

        internal ITentacleServiceDecoratorFactory Build()
            => new ProxyTentacleServiceDecoratorFactory(registeredProxyDecorators);

        public TentacleServiceDecoratorBuilder RegisterProxyDecorator<TService>(Func<TService, TService> proxyDecoratorFactory) where TService : class
        {
            var serviceType = typeof(TService);

            return RegisterProxyDecorator(serviceType, WrapGenericFunc);

            object WrapGenericFunc(object o) => proxyDecoratorFactory((TService)o);
        }

        public TentacleServiceDecoratorBuilder RegisterProxyDecorator(Type serviceType, ProxyDecoratorFactory proxyDecoratorFactory)
        {
            if (registeredProxyDecorators.TryGetValue(serviceType, out var factories))
            {
                // we add each builder at the start so the latest registered decorator wraps the original service
                factories.Insert(0, proxyDecoratorFactory);
            }
            else
            {
                registeredProxyDecorators.Add(serviceType, new List<ProxyDecoratorFactory>
                {
                    proxyDecoratorFactory
                });
            }

            return this;
        }

        class ProxyTentacleServiceDecoratorFactory : ITentacleServiceDecoratorFactory
        {
            readonly Dictionary<Type, List<ProxyDecoratorFactory>> registeredProxyDecorators;

            public ProxyTentacleServiceDecoratorFactory(Dictionary<Type, List<ProxyDecoratorFactory>> registeredProxyDecorators)
            {
                this.registeredProxyDecorators = registeredProxyDecorators;
            }

            public IAsyncClientScriptService Decorate(IAsyncClientScriptService scriptService) => GetDecoratedProxy(scriptService);

            public IAsyncClientScriptServiceV2 Decorate(IAsyncClientScriptServiceV2 scriptService) => GetDecoratedProxy(scriptService);

            public IAsyncClientFileTransferService Decorate(IAsyncClientFileTransferService service) => GetDecoratedProxy(service);

            public IAsyncClientCapabilitiesServiceV2 Decorate(IAsyncClientCapabilitiesServiceV2 service) => GetDecoratedProxy(service);

            T GetDecoratedProxy<T>(T service) where T : class
            {
                var proxiedService = service;

                if (registeredProxyDecorators.TryGetValue(typeof(T), out var builders))
                {
                    foreach (var builder in builders)
                    {
                        // pass the proxied service in and build it
                        proxiedService = (T)builder(proxiedService);
                    }
                }

                //we add the call logging proxy decorator to all services
                proxiedService = MethodLoggingProxyDecorator.Create(proxiedService);

                return proxiedService;
            }
        }
    }
}