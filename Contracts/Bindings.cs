using System;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace Octopus.Shared.Contracts
{
    public class Bindings
    {
        public static CustomBinding CreateDefault()
        {
            var binding = CreateNormal();
            return ApplyMaxPendingChannels(binding);
        }

        static WSHttpBinding CreateNormal()
        {
            var binding = new WSHttpBinding();
            binding.CloseTimeout = TimeSpan.FromSeconds(200);
            binding.HostNameComparisonMode = HostNameComparisonMode.WeakWildcard;
            binding.MaxReceivedMessageSize = 1024 * 1024 * 1024;
            binding.MaxBufferPoolSize = 64 * 1024 * 1024;
            binding.MessageEncoding = WSMessageEncoding.Text;
            binding.OpenTimeout = TimeSpan.FromSeconds(200);
            binding.ReaderQuotas.MaxArrayLength = 128 * 1024;
            binding.ReaderQuotas.MaxStringContentLength = 100 * 1024 * 1024;
            binding.ReceiveTimeout = TimeSpan.FromMinutes(20);
            binding.ReliableSession.Enabled = true;
            binding.ReliableSession.InactivityTimeout = TimeSpan.FromSeconds(60);
            binding.ReliableSession.Ordered = true;
            binding.Security.Mode = SecurityMode.Message;
            binding.Security.Message.ClientCredentialType = MessageCredentialType.Certificate;
            binding.SendTimeout = TimeSpan.FromMinutes(20);

            return binding;
        }

        static CustomBinding ApplyMaxPendingChannels(Binding baseBinding)
        {
            var elements = baseBinding.CreateBindingElements();
            var reliableSessionElement = elements.Find<ReliableSessionBindingElement>();
            
            if (reliableSessionElement != null)
            {
                reliableSessionElement.MaxPendingChannels = 128;

                var newBinding = new CustomBinding(elements);

                newBinding.CloseTimeout = baseBinding.CloseTimeout;
                newBinding.OpenTimeout = baseBinding.OpenTimeout;
                newBinding.ReceiveTimeout = baseBinding.ReceiveTimeout;
                newBinding.SendTimeout = baseBinding.SendTimeout;
                newBinding.Name = baseBinding.Name;
                newBinding.Namespace = baseBinding.Namespace;
                return newBinding;
            }
            
            throw new Exception("the base binding does not have ReliableSessionBindingElement");
        }
    }
}