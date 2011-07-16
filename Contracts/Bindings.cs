using System;
using System.ServiceModel;

namespace Octopus.Shared.Contracts
{
    public class Bindings
    {
        public static WSHttpBinding CreateDefault()
        {
            var binding = new WSHttpBinding();
            binding.CloseTimeout = TimeSpan.FromSeconds(10);
            binding.HostNameComparisonMode = HostNameComparisonMode.WeakWildcard;
            binding.MaxReceivedMessageSize = 1024*1024*1024;
            binding.MaxBufferPoolSize = 64*1024*1024;
            binding.MessageEncoding = WSMessageEncoding.Text;
            binding.OpenTimeout = TimeSpan.FromSeconds(10);
            binding.ReaderQuotas.MaxArrayLength = 128*1024;
            binding.ReaderQuotas.MaxStringContentLength = 10*1024*1024;
            binding.ReceiveTimeout = TimeSpan.FromMinutes(10);
            binding.ReliableSession.Enabled = true;
            binding.ReliableSession.InactivityTimeout = TimeSpan.FromSeconds(60);
            binding.ReliableSession.Ordered = true;
            binding.Security.Mode = SecurityMode.Message;
            binding.Security.Message.ClientCredentialType = MessageCredentialType.Certificate;
            binding.SendTimeout = TimeSpan.FromSeconds(10);
            return binding;
        }
    }
}