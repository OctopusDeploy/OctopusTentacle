using System;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public interface ITentacleExecutionContext
    {
        public string TempDir { get; }
        string TentacleExePath { get; }
    }

    public class E2ETentacleExecutionContext : ITentacleExecutionContext
    {

        public E2ETentacleExecutionContext()
        {
        }

        // TODO luke
        public string TempDir => "/tmp/";

        // TODO luke
        public string TentacleExePath => "/home/auser/Documents/octopus/OctopusTentacle/source/Octopus.Tentacle/bin/net6.0/Tentacle";
    }

    public class ServerTentacleConnectionDetails
    {
        public ServerTentacleConnectionDetails(string apiKey, string httpListenUri, int commsPort)
        {
            ApiKey = apiKey;
            HttpListenUri = httpListenUri;
            CommsPort = commsPort;
        }

        public string? WebSocketUrl { get; set; }
        public string ApiKey { get; }
        public string HttpListenUri { get; }
        public int CommsPort { get; }
        
        public string? ServerCertificateThumbprint { get; set; }

        // public static ServerTentacleConnectionDetails From(OctopusServer octopusServer)
        // {
        //     ServerTentacleConnectionDetails serverTentacleConnectionDetails = new ServerTentacleConnectionDetails(octopusServer.ApiKey, octopusServer.HttpListenUri, octopusServer.CommsPort);
        //     
        //     serverTentacleConnectionDetails.ServerCertificateThumbprint = octopusServer.ServerCertificateThumbprint;
        //     return serverTentacleConnectionDetails;
        // }
    }



}