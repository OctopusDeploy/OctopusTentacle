using System;
using System.Net.Http;
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
#if NETFRAMEWORK
                    var httpHandler = new HttpClientHandler();
                    
                    //we validate the remote certificate has the same thumbprint as what we've recorded
                    //TODO: Put correct thumbprint here
                    httpHandler.ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert is not null && cert.GetCertHashString() == "";
#else
                    var httpHandler = new SocketsHttpHandler
                    {
                        EnableMultipleHttp2Connections = true,
                    };
                    
                    //we validate the remote certificate has the same thumbprint as what we've recorded
                    //TODO: Put correct thumbprint here
                    httpHandler.SslOptions.RemoteCertificateValidationCallback = (_, cert, _, _) => cert is not null && cert.GetCertHashString() == "";
#endif


                    var credentials = CallCredentials.FromInterceptor(async (_, metadata) =>
                    {
                        await Task.CompletedTask;

                        //TODO: Real values here
                        metadata.Add("client-id", Guid.NewGuid().ToString());
                        metadata.Add("authorization", "Bearer ABC123");
                    });

                    var channel = GrpcChannel.ForAddress("http://localhost:8443", new GrpcChannelOptions
                    {
                        HttpHandler = httpHandler,
                        DisposeHttpClient = true,

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