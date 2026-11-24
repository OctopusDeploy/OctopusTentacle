using System.Buffers;
using System.Threading.Channels;
using Grpc.Core;

namespace Octopus.Pipelines.Runner.Gateway;

/// <summary>
/// Runs gRPC calls against a service implementation in the same process: no HTTP/2, sockets or server. The CLI uses
/// it to play the Server, so its jobs go through the same <see cref="GatewayClient"/> and contract as the remote
/// hosts'. Messages still go through each method's marshallers, so the CLI exercises the same serialization, and
/// neither side can mutate a message the other holds.
/// </summary>
/// <remarks>Only duplex streaming is implemented: it's all <c>RunnerGateway</c> uses.</remarks>
/// <example><c>new InProcessCallInvoker(binder => RunnerGateway.BindService(binder, myService))</c></example>
public sealed class InProcessCallInvoker : CallInvoker
{
    readonly Dictionary<string, Delegate> handlers = new(StringComparer.Ordinal);

    /// <param name="bind">Binds services, e.g. with a generated <c>BindService(ServiceBinderBase, impl)</c>.</param>
    public InProcessCallInvoker(Action<ServiceBinderBase> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        bind(new Binder(handlers));
    }

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string? host, CallOptions options)
    {
        if (!handlers.TryGetValue(method.FullName, out var bound) || bound is not DuplexStreamingServerMethod<TRequest, TResponse> handler)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, $"{method.FullName} is not bound in process."));
        }

        var toServer = Channel.CreateUnbounded<TRequest>(new UnboundedChannelOptions { SingleReader = true });
        var toClient = Channel.CreateUnbounded<TResponse>(new UnboundedChannelOptions { SingleReader = true });
        var call = CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken);
        var context = new Context(method.FullName, options.Headers ?? [], call.Token);
        var status = Status.DefaultSuccess;

        _ = Task.Run(async () =>
        {
            try
            {
                await handler(
                    new Reader<TRequest>(toServer.Reader, method.RequestMarshaller, call.Token),
                    new Writer<TResponse>(toClient.Writer, method.ResponseMarshaller),
                    context);
                toClient.Writer.TryComplete();
            }
            catch (Exception e)
            {
                status = e is RpcException rpc
                    ? rpc.Status
                    : new Status(call.IsCancellationRequested ? StatusCode.Cancelled : StatusCode.Unknown, e.Message);
                toClient.Writer.TryComplete(new RpcException(status));
            }
        });

        return new AsyncDuplexStreamingCall<TRequest, TResponse>(
            new ClientWriter<TRequest>(toServer.Writer, method.RequestMarshaller),
            new Reader<TResponse>(toClient.Reader, method.ResponseMarshaller, call.Token),
            Task.FromResult(new Metadata()),
            () => status,
            () => [],
            () =>
            {
                // Disposing the call cancels it on both sides, like a real channel.
                call.Cancel();
                toServer.Writer.TryComplete();
            });
    }

    public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) =>
        throw new NotSupportedException("Only duplex streaming calls run in process.");

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) =>
        throw new NotSupportedException("Only duplex streaming calls run in process.");

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) =>
        throw new NotSupportedException("Only duplex streaming calls run in process.");

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options) =>
        throw new NotSupportedException("Only duplex streaming calls run in process.");

    /// <summary>Serializes and deserializes: the receiver gets its own copy, exactly as it would off the wire.</summary>
    static T Copy<T>(Marshaller<T> marshaller, T message)
    {
        var buffer = new ArrayBufferWriter<byte>();
        marshaller.ContextualSerializer(message, new Serialization(buffer));
        return marshaller.ContextualDeserializer(new Deserialization(buffer.WrittenMemory.ToArray()));
    }

    sealed class Binder(Dictionary<string, Delegate> handlers) : ServiceBinderBase
    {
        public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, DuplexStreamingServerMethod<TRequest, TResponse>? handler)
        {
            if (handler is not null)
            {
                handlers[method.FullName] = handler;
            }
        }
    }

    sealed class Reader<T>(ChannelReader<T> reader, Marshaller<T> marshaller, CancellationToken callToken) : IAsyncStreamReader<T>
    {
        public T Current { get; private set; } = default!;

        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, callToken);
            try
            {
                if (!await reader.WaitToReadAsync(linked.Token) || !reader.TryRead(out var item))
                {
                    return false;
                }

                Current = Copy(marshaller, item);
                return true;
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested)
            {
                // A real channel reports a cancelled call this way too.
                throw new RpcException(new Status(StatusCode.Cancelled, "Call cancelled."));
            }
        }
    }

    sealed class Writer<T>(ChannelWriter<T> writer, Marshaller<T> marshaller) : IServerStreamWriter<T>
    {
        public WriteOptions? WriteOptions { get; set; }

        public Task WriteAsync(T message) => writer.WriteAsync(Copy(marshaller, message)).AsTask();
    }

    sealed class ClientWriter<T>(ChannelWriter<T> writer, Marshaller<T> marshaller) : IClientStreamWriter<T>
    {
        public WriteOptions? WriteOptions { get; set; }

        public Task WriteAsync(T message) => WriteAsync(message, CancellationToken.None);

        public Task WriteAsync(T message, CancellationToken cancellationToken) =>
            writer.WriteAsync(Copy(marshaller, message), cancellationToken).AsTask();

        public Task CompleteAsync()
        {
            writer.TryComplete();
            return Task.CompletedTask;
        }
    }

    sealed class Serialization(ArrayBufferWriter<byte> buffer) : SerializationContext
    {
        public override void SetPayloadLength(int payloadLength)
        {
        }

        public override IBufferWriter<byte> GetBufferWriter() => buffer;

        public override void Complete()
        {
        }

        public override void Complete(byte[] payload) => buffer.Write(payload);
    }

    sealed class Deserialization(byte[] payload) : DeserializationContext
    {
        public override int PayloadLength => payload.Length;

        public override byte[] PayloadAsNewBuffer() => (byte[])payload.Clone();

        public override ReadOnlySequence<byte> PayloadAsReadOnlySequence() => new(payload);
    }

    sealed class Context(string method, Metadata headers, CancellationToken cancellationToken) : ServerCallContext
    {
        protected override string MethodCore => method;

        protected override string HostCore => "in-process";

        protected override string PeerCore => "in-process";

        protected override DateTime DeadlineCore => DateTime.MaxValue;

        protected override Metadata RequestHeadersCore => headers;

        protected override CancellationToken CancellationTokenCore => cancellationToken;

        protected override Metadata ResponseTrailersCore { get; } = [];

        protected override Status StatusCore { get; set; }

        protected override WriteOptions? WriteOptionsCore { get; set; }

        protected override AuthContext AuthContextCore { get; } = new(null, new Dictionary<string, List<AuthProperty>>());

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) =>
            throw new NotSupportedException("Calls made in process can't propagate their context.");

        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    }
}
