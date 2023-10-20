using System;
using System.Collections.Generic;
using System.Linq;
using Autofac;
using Halibut.ServiceModel;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Communications
{
    /// <summary>
    /// This IServiceFactory allows you to resolve service classes from Autofac.
    /// However, before resolving, one or more IAutofacServiceSources must also be registered.
    /// This is so that only explicitly specified services will be resolved, and the types of all messages are known when the service is instantiated.
    /// </summary>
    public class AutofacServiceFactory : IServiceFactory, IServiceRegistration, IDisposable
    {
        // Must never be modified as it is required for backwards compatability in BackwardsCompatibleCapabilitiesV2Decorator
        const string TentacleServiceShuttingDownMessage = "The Tentacle service is shutting down and cannot process this request.";
        readonly ILifetimeScope scope;
        readonly Dictionary<string, KnownService> knownServices = new();

        public AutofacServiceFactory(ILifetimeScope scope, IEnumerable<IAutofacServiceSource> sources)
        {
            this.scope = scope.BeginLifetimeScope(b =>
            {
                foreach (var knownService in sources.SelectMany(x => x.KnownServices.EmptyIfNull()))
                {
                    BuildService(b, knownService);
                }
            });
        }

        void BuildService(ContainerBuilder builder, KnownService knownService)
        {
            builder
                .RegisterType(knownService.ServiceImplementationType)
                .AsSelf()
                .SingleInstance();

            //track the interface types to their known service implementations
            knownServices[knownService.ServiceInterfaceType.Name] = knownService;

            if (knownService.ServiceImplementationType.IsInterface || knownService.ServiceImplementationType.GetInterfaces().IsNullOrEmpty())
            {
                throw new InvalidServiceTypeException(knownService.ServiceImplementationType);
            }
        }

        public IServiceLease CreateService(string serviceName)
        {
            try
            {
                if (knownServices.TryGetValue(serviceName, out var knownService))
                {
                    //because the service implementations are registered `AsSelf()`, we can resolve them directly
                    return new Lease(scope.Resolve(knownService.ServiceImplementationType));
                }

                throw new UnknownServiceNameException(serviceName);
            }
            catch (ObjectDisposedException)
            {
                throw new Exception(TentacleServiceShuttingDownMessage);
            }
        }

        public IReadOnlyList<Type> RegisteredServiceTypes => knownServices.Values.Select(ks => ks.ServiceInterfaceType).ToList();

        class Lease : IServiceLease
        {
            public Lease(object service)
            {
                Service = service;
            }

            public object Service { get; }

            public void Dispose()
            {
                if (Service is IDisposable disposable)
                    disposable.Dispose();
            }
        }

        public void Dispose()
        {
            scope.Dispose();
        }

        public T GetService<T>()
        {
            return scope.Resolve<T>();
        }
    }
}