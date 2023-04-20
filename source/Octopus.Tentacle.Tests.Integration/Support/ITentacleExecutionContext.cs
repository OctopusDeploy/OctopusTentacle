//using System;

//namespace Octopus.Tentacle.Tests.Integration.Support
//{
//    public interface ITentacleExecutionContext
//    {
//        public string TempDir { get; }
//        string TentacleExePath { get; }
//    }

//    public class E2ETentacleExecutionContext : ITentacleExecutionContext
//    {
//        public E2ETestExecutionContextWithLocalExecutables E2ETestExecutionContextWithLocalExecutables;

//        public E2ETentacleExecutionContext(E2ETestExecutionContextWithLocalExecutables e2ETestExecutionContextWithLocalExecutables)
//        {
//            E2ETestExecutionContextWithLocalExecutables = e2ETestExecutionContextWithLocalExecutables;
//        }

//        public string TempDir => new TestDirectory(E2ETestExecutionContextWithLocalExecutables).FullPath;

//        public string TentacleExePath => E2ETestExecutionContextWithLocalExecutables.TentacleExePath;
//    }

//    public class ServerTentacleConnectionDetails
//    {
//        public ServerTentacleConnectionDetails(string apiKey, string httpListenUri, int commsPort)
//        {
//            ApiKey = apiKey;
//            HttpListenUri = httpListenUri;
//            CommsPort = commsPort;
//        }

//        public string? WebSocketUrl { get; set; }
//        public string ApiKey { get; }
//        public string HttpListenUri { get; }
//        public int CommsPort { get; }
//        public string? ServerCertificateThumbprint { get; set; }

//        public static ServerTentacleConnectionDetails From(OctopusServer octopusServer)
//        {
//            ServerTentacleConnectionDetails serverTentacleConnectionDetails = new ServerTentacleConnectionDetails(octopusServer.ApiKey, octopusServer.HttpListenUri, octopusServer.CommsPort);
//            serverTentacleConnectionDetails.WebSocketUrl = octopusServer.WebSocketUrl;
//            serverTentacleConnectionDetails.ServerCertificateThumbprint = octopusServer.ServerCertificateThumbprint;
//            return serverTentacleConnectionDetails;
//        }
//    }



//}