using System;
using Octopus.Client.Model;
using Octopus.Shared.Security;

namespace Octopus.Shared.Endpoints
{
    /// <summary>
    /// Contains the information necessary to communicate with an SSH endpoint
    /// </summary>
    /// <remarks>If a private key file is provided it will be used; otherwise we
    /// fall back to username/password.</remarks>
    public class SshEndpoint : Endpoint, IEndpointWithAccount, IEndpointWithHostname
    {
        public override CommunicationStyle CommunicationStyle
        {
            get { return CommunicationStyle.Ssh; }
        }

        public string Host { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Fingerprint { get; set; }

        [Encrypted]
        public string Password { get; set; }
        public string PrivateKeyFile { get; set; }
        
        [Encrypted]
        public string PrivateKeyPassphrase { get; set; }
        public string AccountId { get; set; }

        public override string ToString()
        {
            var uri = new UriBuilder("ssh", Host) { UserName = Username };
            return uri.ToString();
        }
    }
}
