using System;
using System.Collections.Generic;
using Octopus.Shared.Variables;

namespace Octopus.Shared.Endpoints
{
    /// <summary>
    /// Contains the information necessary to communicate with an FTP site
    /// </summary>
    public class FtpEndpoint : AgentlessEndpoint, IEndpointWithAccount, IEndpointWithHostname
    {
        public FtpEndpoint() : this(new Dictionary<string, Variable>()) { }

        public FtpEndpoint(IDictionary<string, Variable> raw)
            : base(raw)
        {
        }

        public string AccountId { get { return GetEndpointProperty<string>("AccountId"); } set { SetEndpointProperty("AccountId", value); } }
        public string Host { get { return GetEndpointProperty<string>("Host"); } set { SetEndpointProperty("Host", value); } }
        public string Username { get { return GetEndpointProperty<string>("Username"); } set { SetEndpointProperty("Username", value); } }
        public string Password { get { return GetEndpointProperty<string>("Password"); } set { SetEndpointProperty("Password", value, isSensitive: true); } }
        public bool UseFtps { get { return GetEndpointProperty<bool>("UseFtps"); } set { SetEndpointProperty("UseFtps", value); } }
        public int? Port { get { return GetEndpointProperty<int?>("Port"); } set { SetEndpointProperty("Port", value); } }
        public string RootDirectory { get { return GetEndpointProperty<string>("RootDirectory"); } set { SetEndpointProperty("RootDirectory", value); } }
        public bool DeleteDestinationFiles { get { return GetEndpointProperty<bool>("DeleteDestinationFiles"); } set { SetEndpointProperty("DeleteDestinationFiles", value); } }
        public bool UseActiveMode { get { return GetEndpointProperty<bool>("UseActiveMode"); } set { SetEndpointProperty("UseActiveMode", value); } }
        public int? SocketTimeoutMinutes { get { return GetEndpointProperty<int?>("SocketTimeoutMinutes"); } set { SetEndpointProperty("SocketTimeoutMinutes", value); } }

        public override string ToString()
        {
            var uri = new UriBuilder(UseFtps ? "ftps" : "ftp", Host);
            if (!string.IsNullOrEmpty(Username))
                uri.UserName = Username;

            if (Port.HasValue)
                uri.Port = Port.Value;

            return uri.ToString();
        }
    }
}
