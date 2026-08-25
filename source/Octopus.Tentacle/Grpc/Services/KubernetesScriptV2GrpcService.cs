using System.Threading;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Octopus.Tentacle.Contracts.Grpc;
using Octopus.Tentacle.Core.Diagnostics;

namespace Octopus.Tentacle.Grpc.Services
{
    public class KubernetesScriptV2GrpcService : GrpcService
    {
        readonly KubernetesScriptServiceV2.KubernetesScriptServiceV2Client client;

        public KubernetesScriptV2GrpcService(ISystemLog log, GrpcChannel grpcChannel) : base(log)
        {
            client = new KubernetesScriptServiceV2.KubernetesScriptServiceV2Client(grpcChannel);
        }

        protected override async Task Execute(CancellationToken cancellationToken)
        {
            await SubscribeToStartScript(cancellationToken);
        }

        async Task SubscribeToStartScript(CancellationToken cancellationToken)
        {
            //we run this in a background thread so we can have multiple subscribers
            await Task.Run(async () =>
            {
                Log.Verbose($"{nameof(KubernetesScriptV2GrpcService)}.{nameof(SubscribeToStartScript)}");

                using var stream = client.StartScript(cancellationToken: cancellationToken);

                //we do want to block on this
                while (await stream.ResponseStream.MoveNext(cancellationToken))
                {
                    var startCommand = stream.ResponseStream.Current;

                    var response = new KubernetesScriptStatusResponseV2
                    {
                        RequestId = startCommand.RequestId
                    };

#if NETFRAMEWORK
                    await stream.RequestStream.WriteAsync(response);
#else
                    await stream.RequestStream.WriteAsync(response, cancellationToken);
#endif
                }

                //we are done, so stop the request stream
                await stream.RequestStream.CompleteAsync();
            }, cancellationToken);
        }
    }
}