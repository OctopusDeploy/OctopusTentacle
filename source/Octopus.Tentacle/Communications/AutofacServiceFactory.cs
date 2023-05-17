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
    public class AutofacServiceFactory : IServiceFactory, IDisposable
    {
        // Must never be modified as it is required for backwards compatability in BackwardsCompatibleCapabilitiesV2Decorator
        const string TentacleServiceShuttingDownMessage = "The Tentacle service is shutting down and cannot process this request.";
        readonly ILifetimeScope scope;
        readonly Dictionary<string, Type> serviceTypes = new Dictionary<string, Type>();

        public AutofacServiceFactory(ILifetimeScope scope, IEnumerable<IAutofacServiceSource> sources)
        {
            this.scope = scope.BeginLifetimeScope(b =>
            {
                foreach (var service in sources.SelectMany(x => x.ServiceTypes.EmptyIfNull()))
                {
                    BuildService(b, service);
                }
            });
        }

        void BuildService(ContainerBuilder builder, Type serviceType)
        {
            var registrationBuilder = builder.RegisterType(serviceType).SingleInstance();
            var interfaces = serviceType.GetInterfaces();
            if (serviceType.IsInterface || interfaces.IsNullOrEmpty())
            {
                throw new InvalidServiceTypeException(serviceType);
            }
            
            foreach (var face in interfaces)
            {
                serviceTypes[face.Name] = face;
                registrationBuilder.As(face);
            }
        }

        public IServiceLease CreateService(string serviceName)
        {
            try
            {
                if (serviceTypes.TryGetValue(serviceName, out var serviceType))
                {
                    return new Lease(scope.Resolve(serviceType));
                }
                
                throw new UnknownServiceNameException(serviceName);
            }
            catch (ObjectDisposedException)
            {
                throw new Exception(TentacleServiceShuttingDownMessage);
            }
        }

        public IReadOnlyList<Type> RegisteredServiceTypes => serviceTypes.Values.ToList();

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
    }
}