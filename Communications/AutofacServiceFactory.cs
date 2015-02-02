using System;
using Autofac;
using Halibut.ServiceModel;

namespace Octopus.Agent.Communications.TcpServer
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
            var service = scope.ResolveNamed<object>(serviceName);
            return new Lease(service);
        }

        class Lease : IServiceLease
        {
            readonly object service;

            public Lease(object service)
            {
                this.service = service;
            }

            public object Service
            {
                get { return service; }
            }

            public void Dispose()
            {
                var disposable = service as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }
            }
        }
    }
}