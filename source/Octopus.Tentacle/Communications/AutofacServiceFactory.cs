using System;
using System.Collections.Generic;
using System.IdentityModel;
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
        readonly Dictionary<string, KnownService> knownServices;

        public AutofacServiceFactory(ILifetimeScope scope, IEnumerable<IAutofacServiceSource> sources)
        {
            this.scope = scope;

            //track the contract type to their known service implementations
            //the contract type is the one that is sent across the wire (typically an IService sync contract)
            knownServices = sources
                .SelectMany(x => x.KnownServices.EmptyIfNull())
                .ToDictionary(ks => ks.ServiceContractType.Name);
        }

        public IServiceLease CreateService(string serviceName)
        {
            try
            {
                if (knownServices.TryGetValue(serviceName, out var knownService))
                {
                    //create a nested scope for the service lease. Halibut will automatically dispose of this lease at the end of the RPC call,
                    //so pass the nested scope to the lease for disposal
                    var nestedScope = scope.BeginLifetimeScope();

                    //because the service implementations are registered `AsSelf()`, we can resolve them directly from the nested scope
                    return new Lease(nestedScope.Resolve(knownService.ServiceImplementationType), nestedScope);
                }

                throw new UnknownServiceNameException(serviceName);
            }
            catch (ObjectDisposedException)
            {
                throw new Exception(TentacleServiceShuttingDownMessage);
            }
        }

        public IReadOnlyList<Type> RegisteredServiceTypes => knownServices.Values.Select(ks => ks.ServiceContractType).ToList();

        class Lease : IServiceLease
        {
            readonly ILifetimeScope scope;

            public Lease(object service, ILifetimeScope scope)
            {
                this.scope = scope;
                Service = service;
            }

            public object Service { get; }

            public void Dispose()
            {
                scope.Dispose();
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