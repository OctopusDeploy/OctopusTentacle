using System;
using System.ServiceModel;

namespace Octopus.Shared.Contracts
{
    public class Bindings
    {
        public static WSHttpBinding CreateDefault()
        {
            var binding = new WSHttpBinding();
            binding.CloseTimeout = TimeSpan.FromSeconds(200);
            binding.HostNameComparisonMode = HostNameComparisonMode.WeakWildcard;
            binding.MaxReceivedMessageSize = 1024*1024*1024;
            binding.MaxBufferPoolSize = 64*1024*1024;
            binding.OpenTimeout = TimeSpan.FromSeconds(200);
            binding.ReaderQuotas.MaxArrayLength = 128*1024;
            binding.ReaderQuotas.MaxStringContentLength = 10*1024*1024;
            binding.ReceiveTimeout = TimeSpan.FromMinutes(20);
            binding.ReliableSession.Enabled = true;
            binding.ReliableSession.InactivityTimeout = TimeSpan.FromSeconds(60);
            binding.ReliableSession.Ordered = true;
            binding.Security.Mode = SecurityMode.Message;
            binding.Security.Message.ClientCredentialType = MessageCredentialType.Certificate;
            binding.SendTimeout = TimeSpan.FromSeconds(60);
            return binding;
        }
    }
}