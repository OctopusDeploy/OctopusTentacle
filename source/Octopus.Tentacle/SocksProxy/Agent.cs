using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Octopus.Tentacle.SocksProxy
{

    public class Agent
    {
        readonly string connectUrl;
        private const int BufferSize = 8192;

        private const string AgentId = "agent-001";
        private const int ConnectionCount = 5;
        private static readonly SemaphoreSlim ConnectionSemaphore = new(ConnectionCount, ConnectionCount);
        private static readonly ConcurrentDictionary<Guid, Task> ActiveConnections = new();


        public Agent(string connectUrl)
        {
            this.connectUrl = connectUrl;
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .Enrich.WithProperty("Application", "SocksAgent")
                .Enrich.WithProperty("PoC", "Proxy")
                .CreateLogger();
        }

        public async Task StartAsync()
        {
            for (var i = 0; i < ConnectionCount; i++)
            {
                _ = StartConnection();
            }

            // Monitor connections and ensure we always have the required number
            await MonitorConnectionsAsync();
        }


        async Task MonitorConnectionsAsync()
        {
            while (true)
            {
                // Check current connection count
                var currentCount = ActiveConnections.Count;
                Log.Verbose("Current active connections: {ConnectionCount}", currentCount);

                // If we have fewer than required, start new ones
                if (currentCount < ConnectionCount)
                {
                    var connectionsToAdd = ConnectionCount - currentCount;
                    Log.Information("Adding {Count} new connections", connectionsToAdd);

                    for (int i = 0; i < connectionsToAdd; i++)
                    {
                        _ = StartConnection();
                    }
                }

                // Remove completed tasks
                foreach (var connection in ActiveConnections.Where(c => c.Value.IsCompleted).ToList())
                {
                    if (ActiveConnections.TryRemove(connection.Key, out _))
                    {
                        Log.Debug("Removed completed connection {ConnectionId}", connection.Key);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }

        private async Task StartConnection()
        {
            var connectionId = Guid.NewGuid();

            var connectionTask = EstablishConnectionAsync(connectionId);
            ActiveConnections[connectionId] = connectionTask;

            await connectionTask;

            // When connection completes, remove it from active connections
            ActiveConnections.TryRemove(connectionId, out _);
            ConnectionSemaphore.Release();
        }

        private async Task EstablishConnectionAsync(Guid connectionId)
        {
            await ConnectionSemaphore.WaitAsync();

            try
            {
                Log.Information("Connection {ConnectionId} connecting to proxy at {ForwardingProxyUrl}",
                    connectionId, connectUrl);

                var websocket = new ClientWebSocket();
                websocket.Options.SetRequestHeader("X-Agent-ID", $"{AgentId}-{connectionId}");
                websocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

                await websocket.ConnectAsync(new Uri(connectUrl), CancellationToken.None);

                Log.Information("Connection {ConnectionId} established", connectionId);

                using var websocketStream = new ClientWebSocketStream(websocket);

                await ProxyData(websocketStream, $"{AgentId}-{connectionId}");

                Log.Information("Connection {ConnectionId} ended", connectionId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in connection {ConnectionId}", connectionId);
            }
        }


        private static async Task ProxyData(Stream stream, string clientInfo)
        {
            // SOCKS5 initialization
            if (!await HandleSocks5InitializationAsync(stream, clientInfo))
            {
                Log.Warning("Failed SOCKS5 initialization for client: {ClientInfo}", clientInfo);
                return;
            }

            // SOCKS5 request
            var request = await ReadSocks5RequestAsync(stream, clientInfo);
            if (request == null)
            {
                Log.Warning("Failed to read SOCKS5 request from client: {ClientInfo}", clientInfo);
                return;
            }

            // Handle connection request
            await HandleConnectionRequestAsync(stream, request, clientInfo);
        }

        private static async Task<bool> HandleSocks5InitializationAsync(Stream stream, string clientInfo)
        {
            var buffer = new byte[256];
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

            if (bytesRead < 2 || buffer[0] != 0x05)
            {
                Log.Warning("Invalid SOCKS5 initialization from client {ClientInfo}: Not SOCKS5", clientInfo);
                return false;
            }

            int methodCount = buffer[1];
            if (bytesRead < 2 + methodCount)
            {
                Log.Warning("Invalid SOCKS5 initialization from client {ClientInfo}: Incomplete methods", clientInfo);
                return false;
            }

            // Check if client supports no authentication (0x00)
            bool noAuthSupported = false;
            for (int i = 0; i < methodCount; i++)
            {
                if (buffer[2 + i] == 0x00)
                {
                    noAuthSupported = true;
                    break;
                }
            }

            if (!noAuthSupported)
            {
                // No acceptable authentication methods
                await stream.WriteAsync(new byte[] { 0x05, 0xFF }, 0, 2);
                Log.Warning("Client {ClientInfo} doesn't support no-auth method", clientInfo);
                return false;
            }

            // Respond with no authentication method selected
            await stream.WriteAsync(new byte[] { 0x05, 0x00 }, 0, 2);
            Log.Debug("SOCKS5 initialization successful for client {ClientInfo}", clientInfo);
            return true;
        }

        private static async Task<Socks5Request?> ReadSocks5RequestAsync(Stream stream, string clientInfo)
        {
            var buffer = new byte[256];
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

            if (bytesRead < 4 || buffer[0] != 0x05)
            {
                Log.Warning("Invalid SOCKS5 request from client {ClientInfo}", clientInfo);
                return null;
            }

            var request = new Socks5Request
            {
                Command = buffer[1],
                AddressType = buffer[3]
            };

            // Parse address based on address type
            switch (request.AddressType)
            {
                case 0x01: // IPv4
                    if (bytesRead < 10) return null;
                    request.DestinationAddress = new IPAddress(new byte[] { buffer[4], buffer[5], buffer[6], buffer[7] }).ToString();
                    request.DestinationPort = (ushort)((buffer[8] << 8) + buffer[9]);
                    break;

                case 0x03: // Domain name
                    int domainLength = buffer[4];
                    if (bytesRead < 5 + domainLength + 2) return null;
                    request.DestinationAddress = System.Text.Encoding.ASCII.GetString(buffer, 5, domainLength);
                    request.DestinationPort = (ushort)((buffer[5 + domainLength] << 8) + buffer[5 + domainLength + 1]);
                    break;

                case 0x04: // IPv6
                    if (bytesRead < 22) return null;
                    byte[] ipv6Bytes = new byte[16];
                    Array.Copy(buffer, 4, ipv6Bytes, 0, 16);
                    request.DestinationAddress = new IPAddress(ipv6Bytes).ToString();
                    request.DestinationPort = (ushort)((buffer[20] << 8) + buffer[21]);
                    break;

                default:
                    Log.Warning("Unsupported address type: {AddressType} from client {ClientInfo}", request.AddressType, clientInfo);
                    await SendSocks5Response(stream, 0x08); // Address type not supported
                    return null;
            }

            Log.Information("SOCKS5 request from client {ClientInfo}: Command={Command}, Address={Address}, Port={Port}",
                clientInfo, request.Command, request.DestinationAddress, request.DestinationPort);

            return request;
        }

        private static async Task HandleConnectionRequestAsync(Stream clientStream, Socks5Request request, string clientInfo)
        {
            // Only support CONNECT command (0x01)
            if (request.Command != 0x01)
            {
                Log.Warning("Unsupported SOCKS5 command: {Command} from client {ClientInfo}", request.Command, clientInfo);
                await SendSocks5Response(clientStream, 0x07); // Command not supported
                return;
            }

            // Resolve the destination address
            IPAddress[] destinationAddresses;
            try
            {
                if (IPAddress.TryParse(request.DestinationAddress, out var ipAddress))
                {
                    destinationAddresses = new[] { ipAddress };
                }
                else
                {
                    // Perform DNS resolution
                    destinationAddresses = await Dns.GetHostAddressesAsync(request.DestinationAddress!);
                    if (destinationAddresses.Length == 0)
                    {
                        Log.Warning("Could not resolve destination address: {Address} for client {ClientInfo}",
                            request.DestinationAddress, clientInfo);
                        await SendSocks5Response(clientStream, 0x04); // Host unreachable
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error resolving destination address: {Address} for client {ClientInfo}",
                    request.DestinationAddress, clientInfo);
                await SendSocks5Response(clientStream, 0x04); // Host unreachable
                return;
            }

            // Connect to the destination server
            using var destinationClient = new TcpClient();

            try
            {
                await destinationClient.ConnectAsync(destinationAddresses, request.DestinationPort);

                var destinationEndpoint = destinationClient.Client.RemoteEndPoint as IPEndPoint;
                var destinationInfo = destinationEndpoint?.ToString() ?? "unknown";

                // Send success response
                //await SendSocks5Response(clientStream, 0x00);
                await SendSocks5Response(clientStream, 0x00, destinationClient.Client.LocalEndPoint as IPEndPoint);

                // Start relaying data between client and destination
                Log.Information("Connected to destination {DestAddress}:{DestPort} for client {ClientInfo}",
                    request.DestinationAddress, request.DestinationPort, clientInfo);

                using var destinationStream = destinationClient.GetStream();
                await Task.WhenAny(
                    ForwardDataAsync(clientStream, destinationStream, $"client({clientInfo}) -> destination({destinationInfo})"),
                    ForwardDataAsync(destinationStream, clientStream, $"destination({destinationInfo}) -> client({clientInfo})")
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error connecting to destination {DestAddress}:{DestPort} for client {ClientInfo}",
                    request.DestinationAddress, request.DestinationPort, clientInfo);
                await SendSocks5Response(clientStream, 0x05); // Connection refused
            }
        }

        private static async Task SendSocks5Response(Stream stream, byte status, IPEndPoint? localEndPoint = null)
        {
            // Create response buffer - the size depends on the address type
            byte[] response;
            byte addressType = 0x01; // Default to IPv4

            if (localEndPoint != null && localEndPoint.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                addressType = 0x04; // IPv6
                response = new byte[22]; // 4 bytes header + 16 bytes IPv6 address + 2 bytes port
            }
            else
            {
                addressType = 0x01; // IPv4
                response = new byte[10]; // 4 bytes header + 4 bytes IPv4 address + 2 bytes port
            }

            response[0] = 0x05; // SOCKS version
            response[1] = status; // Status code
            response[2] = 0x00; // Reserved
            response[3] = addressType;

            if (localEndPoint != null && status == 0x00)
            {
                // Fill in the bound address and port
                byte[] addressBytes = localEndPoint.Address.GetAddressBytes();
                Array.Copy(addressBytes, 0, response, 4, addressBytes.Length);

                ushort port = (ushort)localEndPoint.Port;

                // Port is always the last 2 bytes of the response
                response[response.Length - 2] = (byte)(port >> 8);
                response[response.Length - 1] = (byte)(port & 0xFF);
            }

            await stream.WriteAsync(response, 0, response.Length);
        }

        private static async Task ForwardDataAsync(Stream from, Stream to, string direction)
        {
            var sleepTime = TimeSpan.FromSeconds(5);
            byte[] buffer = new byte[BufferSize];

            try
            {
                while (true)
                {
                    var bytesRead = await from.ReadAsync(buffer, 0, buffer.Length);

                    await to.WriteAsync(buffer, 0, bytesRead);
                    Log.Debug("Forward {BytesRead} bytes {Direction} ", bytesRead, direction);

                    if (bytesRead == 0)
                    {
                        Log.Debug("No more data to read {Direction}. Sleeping for {SleepTime}", direction, sleepTime);
                        break;
                    }
                }
            }
            catch (IOException)
            {
                Log.Debug("IOException Expected when the other stream was closed");
            }
            catch (ObjectDisposedException)
            {
                Log.Debug("ObjectDisposedException Expected when the other stream was closed");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error relaying data {Direction}", direction);
            }
        }
    }

    class Socks5Request
    {
        public byte Command { get; set; }
        public byte AddressType { get; set; }
        public string? DestinationAddress { get; set; }
        public ushort DestinationPort { get; set; }
    }
}