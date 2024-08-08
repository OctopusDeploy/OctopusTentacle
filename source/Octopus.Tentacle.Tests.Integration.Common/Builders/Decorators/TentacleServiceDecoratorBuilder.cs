using System;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators.Proxies;

namespace Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators
{
    public delegate T Decorator<T>(T service);

    public delegate object ProxyDecoratorFactory(object service);

    public class TentacleServiceDecoratorBuilder
    {
        private readonly List<Decorator<IAsyncClientScriptService>> scriptServiceDecorator = new ();
        private readonly List<Decorator<IAsyncClientScriptServiceV2>> scriptServiceV2Decorator = new ();
        private readonly List<Decorator<IAsyncClientKubernetesScriptServiceV1>> kubernetesScriptServiceV1Decorator = new ();
        private readonly List<Decorator<IAsyncClientFileTransferService>> fileTransferServiceDecorator = new ();
        private readonly List<Decorator<IAsyncClientCapabilitiesServiceV2>> capabilitiesServiceV2Decorator = new ();

        readonly Dictionary<Type, List<ProxyDecoratorFactory>> registeredProxyDecorators = new();

        public TentacleServiceDecoratorBuilder DecorateScriptServiceWith(Decorator<IAsyncClientScriptService> scriptServiceDecorator)
        {
            this.scriptServiceDecorator.Add(scriptServiceDecorator);
            return this;
        }

        public TentacleServiceDecoratorBuilder DecorateScriptServiceV2With(Decorator<IAsyncClientScriptServiceV2> scriptServiceV2Decorator)
        {
            this.scriptServiceV2Decorator.Add(scriptServiceV2Decorator);
            return this;
        }
        
        public TentacleServiceDecoratorBuilder DecorateKubernetesScriptServiceV1With(Decorator<IAsyncClientKubernetesScriptServiceV1> kubernetesScriptServiceV1Decorator)
        {
            this.kubernetesScriptServiceV1Decorator.Add(kubernetesScriptServiceV1Decorator);
            return this;
        }

        public TentacleServiceDecoratorBuilder DecorateAllScriptServicesWith(Action<UniversalScriptServiceDecoratorBuilder> universalScriptServiceDecorator)
        {
            var b = new UniversalScriptServiceDecoratorBuilder();
            universalScriptServiceDecorator(b);
            b.Build().Decorate(this);
            return this;
        }

        public TentacleServiceDecoratorBuilder DecorateScriptServiceV2With(Action<ScriptServiceV2DecoratorBuilder> scriptServiceV2Decorator)
        {
            var b = new ScriptServiceV2DecoratorBuilder();
            scriptServiceV2Decorator(b);
            this.DecorateScriptServiceV2With(b.Build());
            return this;
        }
        
        public TentacleServiceDecoratorBuilder DecorateKubernetesScriptServiceV1With(Action<KubernetesScriptServiceV1DecoratorBuilder> kubernetesScriptServiceV1Decorator)
        {
            var b = new KubernetesScriptServiceV1DecoratorBuilder();
            kubernetesScriptServiceV1Decorator(b);
            DecorateKubernetesScriptServiceV1With(b.Build());
            return this;
        }

        public TentacleServiceDecoratorBuilder DecorateFileTransferServiceWith(Decorator<IAsyncClientFileTransferService> fileTransferServiceDecorator)
        {
            this.fileTransferServiceDecorator.Add(fileTransferServiceDecorator);
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
            this.capabilitiesServiceV2Decorator.Add(capabilitiesServiceV2Decorator);
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
            // Eventually the proxy tentacle service decorator will only work on all methods
            // e.g. for logging or counting. It will always make sense for this decorator to be called first.
            var genericDecorators = new ProxyTentacleServiceDecoratorFactory(registeredProxyDecorators);

            var perMethodDecorators = new CombinePerServiceTentacleServiceDecoratorFactory(
                Combine(scriptServiceDecorator), 
                Combine(scriptServiceV2Decorator), 
                Combine(kubernetesScriptServiceV1Decorator), 
                Combine(fileTransferServiceDecorator), 
                Combine(capabilitiesServiceV2Decorator));

            return new CombiningTentacleServiceDecoratorFactory(new List<ITentacleServiceDecoratorFactory>(){genericDecorators, perMethodDecorators});
        }

        static Decorator<T> Combine<T>(List<Decorator<T>> chain) where T : class
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

        class CombinePerServiceTentacleServiceDecoratorFactory : ITentacleServiceDecoratorFactory
        {
            readonly Decorator<IAsyncClientScriptService> scriptServiceDecorator;
            readonly Decorator<IAsyncClientScriptServiceV2> scriptServiceV2Decorator;
            readonly Decorator<IAsyncClientKubernetesScriptServiceV1> kubernetesScriptServiceV1Decorator;
            readonly Decorator<IAsyncClientFileTransferService> fileTransferServiceDecorator;
            readonly Decorator<IAsyncClientCapabilitiesServiceV2> capabilitiesServiceV2Decorator;

            public CombinePerServiceTentacleServiceDecoratorFactory(
                Decorator<IAsyncClientScriptService> scriptServiceDecorator,
                Decorator<IAsyncClientScriptServiceV2> scriptServiceV2Decorator,
                Decorator<IAsyncClientKubernetesScriptServiceV1> kubernetesScriptServiceV1Decorator,
                Decorator<IAsyncClientFileTransferService> fileTransferServiceDecorator,
                Decorator<IAsyncClientCapabilitiesServiceV2> capabilitiesServiceV2Decorator)
            {
                this.scriptServiceDecorator = scriptServiceDecorator;
                this.scriptServiceV2Decorator = scriptServiceV2Decorator;
                this.fileTransferServiceDecorator = fileTransferServiceDecorator;
                this.capabilitiesServiceV2Decorator = capabilitiesServiceV2Decorator;
                this.kubernetesScriptServiceV1Decorator = kubernetesScriptServiceV1Decorator;
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

            public IAsyncClientKubernetesScriptServiceV1 Decorate(IAsyncClientKubernetesScriptServiceV1 service)
            {
                return kubernetesScriptServiceV1Decorator(service);
            }
        }

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
            public IAsyncClientKubernetesScriptServiceV1 Decorate(IAsyncClientKubernetesScriptServiceV1 service) => GetDecoratedProxy(service);

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
