#if !NETFRAMEWORK
using System;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Halibut;

namespace Octopus.Tentacle.Communications.gRPC
{
    public class GreeterService: Greeter.GreeterBase
    {
        readonly HalibutRuntime halibut;

        public GreeterService(HalibutRuntime halibut)
        {
            this.halibut = halibut;
        }
        
        
        public override Task<HelloReply> SayHello(
            HelloRequest request, ServerCallContext context)
        {
            var client = halibut.CreateAsyncClient<IMyEchoService, IAsyncClientMyEchoService>(HalibutInitializer.ServiceEndPoints.First());
            
            Task.Run(async () =>
            {
                await client.SayHelloAsync("Hello from: " + request.Name);
            });
            
            return Task.FromResult(new HelloReply
            {
                Message = "Hello " + request.Name
            });
        }
    }
}
#endif