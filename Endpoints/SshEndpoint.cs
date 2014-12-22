using System;
using System.Collections.Generic;
using Octopus.Shared.Variables;

namespace Octopus.Shared.Endpoints
{
    /// <summary>
    /// Contains the information necessary to communicate with an SSH endpoint
    /// </summary>
    /// <remarks>If a private key file is provided it will be used; otherwise we
    /// fall back to username/password.</remarks>
    public class SshEndpoint : AgentlessEndpoint, IEndpointWithAccount, IEndpointWithHostname
    {
        [Obsolete("Serialization constructor")]
        public SshEndpoint() : this(new Dictionary<string, Variable>()) { }

        public SshEndpoint(IDictionary<string, Variable> raw)
            : base(raw)
        {
        }

        public string Host { get { return GetEndpointProperty<string>(); } set { SetEndpointProperty(value); } }
        public int Port { get { return GetEndpointProperty<int>(); } set { SetEndpointProperty(value); } }
        public string Username { get { return GetEndpointProperty<string>(); } set { SetEndpointProperty(value); } }
        public string Fingerprint { get { return GetEndpointProperty<string>(); } set { SetEndpointProperty(value); } }
        public string Password { get { return GetEndpointProperty<string>(); } set { SetEndpointProperty(value, isSensitive: true); } }
        public string PrivateKeyFile { get { return GetEndpointProperty<string>(); } set { SetEndpointProperty(value, isSensitive: true); } }
        public string PrivateKeyPassphrase { get { return GetEndpointProperty<string>(); } set { SetEndpointProperty(value, isSensitive: true); } }
        public string AccountId { get { return GetEndpointProperty<string>(); } set { SetEndpointProperty(value); } }

        public override string ToString()
        {
            var uri = new UriBuilder("ssh", Host) { UserName = Username };
            return uri.ToString();
        }
    }
}
