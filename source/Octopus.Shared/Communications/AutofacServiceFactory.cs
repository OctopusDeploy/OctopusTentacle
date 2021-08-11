using System;
using System.Collections.Generic;
using System.Linq;
using Autofac;
using Halibut.ServiceModel;
using Octopus.CoreUtilities.Extensions;

namespace Octopus.Shared.Communications
{
    public interface IAutofacServiceSource
    {
        IEnumerable<Type> GetServices();
    }
    
    public class AutofacServiceFactory : IServiceFactory, IDisposable
    {
        readonly ILifetimeScope scope;
        readonly HashSet<Type> serviceTypes = new HashSet<Type>();

        public AutofacServiceFactory(ILifetimeScope scope, IEnumerable<IAutofacServiceSource> sources)
        {
            this.scope = scope.BeginLifetimeScope(b =>
            {
                // ReSharper disable once ConstantNullCoalescingCondition : if GetServices returns null, this will throw
                foreach (var service in sources.SelectMany(x => x.GetServices() ?? new Type[] {}))
                {
                    BuildService(b, service);
                }
            });
        }

        void BuildService(ContainerBuilder builder, Type serviceType)
        {
            var reg = builder.RegisterType(serviceType).SingleInstance();
            var interfaces = serviceType.GetInterfaces();
            if (serviceType.IsInterface || interfaces.IsNullOrEmpty())
            {
                throw new Exception("Service type must be a class that implements an interface");
            }
            
            foreach (var face in interfaces)
            {
                reg = reg.Named(face.Name, typeof(object));
                serviceTypes.Add(face);
            }
        }

        public IServiceLease CreateService(string serviceName)
        {
            try
            {
                var service = scope.ResolveNamed<object>(serviceName);
                return new Lease(service);
            }
            catch (ObjectDisposedException)
            {
                throw new Exception("The Tentacle service is shutting down and cannot process this request.");
            }
        }

        public IReadOnlyList<Type> RegisteredServiceTypes => serviceTypes.ToList();

        class Lease : IServiceLease
        {
            public Lease(object service)
            {
                Service = service;
            }

            public object Service { get; }

            public void Dispose()
            {
                var disposable = Service as IDisposable;
                if (disposable != null)
                    disposable.Dispose();
            }
        }

        public void Dispose()
        {
            scope.Dispose();
        }
    }
}