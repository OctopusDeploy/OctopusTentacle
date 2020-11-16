using System;
using Autofac;
using Halibut.ServiceModel;

namespace Octopus.Shared.Communications
{
    public class AutofacServiceFactory : IServiceFactory
    {
        readonly ILifetimeScope scope;

        public AutofacServiceFactory(ILifetimeScope scope)
        {
            this.scope = scope;
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
    }
}