using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using Halibut;
using Octopus.Tentacle.Contracts.Grpc;
using Octopus.Tentacle.Core.Diagnostics;
using HalibutScriptServiceV2 = Octopus.Tentacle.Core.Services.Scripts.ScriptServiceV2;
using HalibutScriptStatusResponseV2 = Octopus.Tentacle.Contracts.ScriptServiceV2.ScriptStatusResponseV2;
using HalibutStartScriptCommandV2 = Octopus.Tentacle.Contracts.ScriptServiceV2.StartScriptCommandV2;
using HalibutScriptStatusRequestV2 = Octopus.Tentacle.Contracts.ScriptServiceV2.ScriptStatusRequestV2;
using HalibutCancelScriptCommandV2 = Octopus.Tentacle.Contracts.ScriptServiceV2.CancelScriptCommandV2;
using HalibutCompleteScriptCommandV2 = Octopus.Tentacle.Contracts.ScriptServiceV2.CompleteScriptCommandV2;

using HalibutProcessOutputSource = Octopus.Tentacle.Contracts.ProcessOutputSource;
using HalibutProcessState = Octopus.Tentacle.Contracts.ProcessState;
using HalibutScriptTicket = Octopus.Tentacle.Contracts.ScriptTicket;
using HalibutScriptFile = Octopus.Tentacle.Contracts.ScriptFile;
using HalibutScriptType= Octopus.Tentacle.Contracts.ScriptType;

using GrpcProcessOutput = Octopus.Tentacle.Contracts.Grpc.ProcessOutput;
using GrpcProcessOutputSource = Octopus.Tentacle.Contracts.Grpc.ProcessOutputSource;
using GrpcProcessState = Octopus.Tentacle.Contracts.Grpc.ProcessState;
using GrpcScriptIsolationLevel = Octopus.Tentacle.Contracts.Grpc.ScriptIsolationLevel;
using GrpcScriptTicket = Octopus.Tentacle.Contracts.Grpc.ScriptTicket;

namespace Octopus.Tentacle.Grpc.Services
{
    public class ScriptServiceV2GrpcService : GrpcService
    {
        readonly HalibutScriptServiceV2 halibutScriptServiceV2;
        readonly ScriptServiceV2.ScriptServiceV2Client client;

        public ScriptServiceV2GrpcService(ISystemLog log, GrpcChannel channel, HalibutScriptServiceV2 halibutScriptServiceV2)
            : base(log)
        {
            this.halibutScriptServiceV2 = halibutScriptServiceV2;
            client = new ScriptServiceV2.ScriptServiceV2Client(channel);
        }

        protected override async Task Execute(CancellationToken cancellationToken)
        {
            await SubscribeToStream(
                ct => client.SubscribeToUnaryCommands(cancellationToken: ct),
                async (incomingStream, ct) =>
                {
                    var scriptResponse = await HandleIncomingStream(incomingStream, ct);

                    var grpcResponse = ConvertScriptResponseToGrpcResponse(scriptResponse);

                    var outgoingStream = new ClientToServerStream
                    {
                        RequestId = incomingStream.RequestId,
                    };

                    AttachResponseToOutgoingStream(outgoingStream, grpcResponse);

                    return outgoingStream;
                },
                nameof(ScriptServiceV2.ScriptServiceV2Client.SubscribeToUnaryCommands), 
                cancellationToken);
        }

        static object? ConvertScriptResponseToGrpcResponse(object? scriptResponse)
        {
            switch (scriptResponse)
            {
                case HalibutScriptStatusResponseV2 scriptStatusResponseV2:
                    var response = new ScriptStatusResponseV2
                    {
                        ScriptTicket = new GrpcScriptTicket { TaskId = scriptStatusResponseV2.Ticket.TaskId },
                        State = scriptStatusResponseV2.State switch
                        {
                            HalibutProcessState.Pending => GrpcProcessState.Pending,
                            HalibutProcessState.Running => GrpcProcessState.Running,
                            HalibutProcessState.Complete => GrpcProcessState.Complete,
                            _ => throw new ArgumentOutOfRangeException()
                        },
                        ExitCode = scriptStatusResponseV2.ExitCode,
                        NextLogSequence = scriptStatusResponseV2.NextLogSequence
                    };

                    response.Logs.AddRange(scriptStatusResponseV2.Logs
                        .Select(po => new GrpcProcessOutput
                        {
                            Source = po.Source switch
                            {
                                HalibutProcessOutputSource.StdOut => GrpcProcessOutputSource.Stdout,
                                HalibutProcessOutputSource.StdErr => GrpcProcessOutputSource.Stderr,
                                HalibutProcessOutputSource.Debug => GrpcProcessOutputSource.Debug,
                                _ => throw new ArgumentOutOfRangeException()
                            },
                            Occurred = po.Occurred.ToTimestamp(),
                            Text = po.Text
                        }));

                    return response;

                case null:
                    return null;
                
                default:
                    throw new ArgumentException($"Unknown script response object {scriptResponse.GetType().Name}");
            }
        }

        static void AttachResponseToOutgoingStream(ClientToServerStream outgoingStream, object? response)
        {
            switch (response)
            {
                case ScriptStatusResponseV2 statusResponse:
                    outgoingStream.ScriptStatusResponseV2 = statusResponse;
                    break;
                case null:
                    outgoingStream.Void = new VoidUnaryResponseV2();
                    break;
                default:
                    throw new ArgumentException($"Unknown gRPC response object {response.GetType().Name}");
            }
        }

        async Task<object?> HandleIncomingStream(ServerToClientStream incomingStream, CancellationToken cancellationToken)
        {
            switch (incomingStream.RequestCase)
            {
                case ServerToClientStream.RequestOneofCase.StartScriptCommand:
                    var startCommand = incomingStream.StartScriptCommand;
                    
                    var scriptStartCommand = new HalibutStartScriptCommandV2(
                        startCommand.ScriptBody, 
                        startCommand.ScriptIsolationLevel switch
                        {
                            GrpcScriptIsolationLevel.NoIsolation => Contracts.ScriptIsolationLevel.NoIsolation,
                            GrpcScriptIsolationLevel.FullIsolation => Contracts.ScriptIsolationLevel.FullIsolation,
                            _ => throw new ArgumentOutOfRangeException()
                        },
                        startCommand.ScriptIsolationMutexTimeout.ToTimeSpan(),
                        startCommand.HasIsolationMutexName ? startCommand.IsolationMutexName : null,
                        startCommand.Arguments.ToArray(),
                        startCommand.TaskId,
                        new HalibutScriptTicket(startCommand.ScriptTicket.TaskId),
                        startCommand.DurationToWaitForScriptFinish?.ToTimeSpan()
                    );

                    foreach (var kvp in startCommand.Scripts)
                    {
#if NETFRAMEWORK
                        scriptStartCommand.Scripts.Add((HalibutScriptType)System.Enum.Parse(typeof(HalibutScriptType), kvp.Key, true), kvp.Value);
                        #else
                        scriptStartCommand.Scripts.Add(System.Enum.Parse<HalibutScriptType>(kvp.Key, true), kvp.Value);
#endif
                    }
                    
                    scriptStartCommand.Files.AddRange(startCommand.Files.Select(f =>
                        new HalibutScriptFile(f.Name, DataStream.FromBytes(f.DataStream.ToByteArray()), f.EncryptionPassword)
                    ));

                    //execute the same start script command logic
                    return await halibutScriptServiceV2.StartScriptAsync(scriptStartCommand, cancellationToken);
                
                case ServerToClientStream.RequestOneofCase.ScriptStatusRequest:
                    var request = incomingStream.ScriptStatusRequest;

                    var scriptRequest = new HalibutScriptStatusRequestV2(
                        new HalibutScriptTicket(request.ScriptTicket.TaskId),
                        request.LastLogSequence);

                    return await halibutScriptServiceV2.GetStatusAsync(scriptRequest, cancellationToken);
                case ServerToClientStream.RequestOneofCase.CancelScriptCommand:
                    var cancelCommand = incomingStream.CancelScriptCommand;

                    var scriptCancelCommand = new HalibutCancelScriptCommandV2(new HalibutScriptTicket(cancelCommand.ScriptTicket.TaskId), cancelCommand.LastLogSequence);

                    return await halibutScriptServiceV2.CancelScriptAsync(scriptCancelCommand, cancellationToken);
                case ServerToClientStream.RequestOneofCase.CompleteScriptCommand:
                    var completeCommand = incomingStream.CompleteScriptCommand;
                    var scriptCompleteCommand = new HalibutCompleteScriptCommandV2(new HalibutScriptTicket(completeCommand.ScriptTicket.TaskId));

                    await halibutScriptServiceV2.CompleteScriptAsync(scriptCompleteCommand, cancellationToken);
                    return null;

                case ServerToClientStream.RequestOneofCase.None:
                default:
                    return null;
            }
        }
    }
}