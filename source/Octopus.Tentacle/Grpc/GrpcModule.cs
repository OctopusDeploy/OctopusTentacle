using System;
using System.Threading.Tasks;
using Autofac;
using Grpc.Core;
using Grpc.Net.Client;

namespace Octopus.Tentacle.Grpc
{
    public class GrpcModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.Register(c =>
                {
                    var credentials = CallCredentials.FromInterceptor(async (_, metadata) =>
                    {
                        await Task.CompletedTask;
                        
                        //TODO: Real values here
                        metadata.Add("client-id", Guid.NewGuid().ToString());
                        metadata.Add("authorization", "Bearer ABC123");
                    });
                    
                    var channel = GrpcChannel.ForAddress("http://localhost:8443", new GrpcChannelOptions
                    {
                        //adds auth
                        Credentials = ChannelCredentials.Create(new SslCredentials(), credentials)
                    });
                    
                    return channel;
                })
                .As<GrpcChannel>()
                .As<ChannelBase>()
                .SingleInstance();

            builder.RegisterAssemblyTypes(ThisAssembly)
                .Where(t => t.IsAssignableTo<IGrpcService>())
                .As<IGrpcService>()
                .SingleInstance();
        }
    }
}