using System;
using System.Collections.Generic;
using Octopus.Platform.Variables;

namespace Octopus.Platform.Model.Endpoints
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

        public string AccountId { get { return GetEndpointProperty<string>(); } set { SetEndpointProperty(value); } }
        public string Host { get { return GetEndpointProperty<string>(); } set { SetEndpointProperty(value); } }
        public string Username  { get { return GetEndpointProperty<string>(); } set {SetEndpointProperty(value);} }
        public string Password  { get { return GetEndpointProperty<string>(); } set {SetEndpointProperty(value, isSensitive: true);} }
        public bool UseFtps  { get { return GetEndpointProperty<bool>(); } set {SetEndpointProperty(value);} }
        public int? Port  { get { return GetEndpointProperty<int?>(); } set {SetEndpointProperty(value);} }
        public string RootDirectory  { get { return GetEndpointProperty<string>(); } set {SetEndpointProperty(value);} }
        public bool DeleteDestinationFiles  { get { return GetEndpointProperty<bool>(); } set {SetEndpointProperty(value);} }
        public bool UseActiveMode  { get { return GetEndpointProperty<bool>(); } set {SetEndpointProperty(value);} }
        public int? SocketTimeoutMinutes  { get { return GetEndpointProperty<int?>(); } set {SetEndpointProperty(value);} }

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
