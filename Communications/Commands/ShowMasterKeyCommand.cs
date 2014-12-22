using System;
using Octopus.Shared.Configuration;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Startup;

namespace Octopus.Shared.Communications.Commands
{
    public class ShowMasterKeyCommand : AbstractStandardCommand
    {
        readonly ILog log;
        readonly Lazy<IMasterKeyConfiguration> masterKeyConfiguration;

        public ShowMasterKeyCommand(
            ILog log,
            Lazy<IMasterKeyConfiguration> masterKeyConfiguration,
            IApplicationInstanceSelector selector)
            : base(selector)
        {
            this.log = log;
            this.masterKeyConfiguration = masterKeyConfiguration;
        }

        protected override void Start()
        {
            base.Start();

            log.Info("Octopus Master Key:");
            var base64 = Convert.ToBase64String(masterKeyConfiguration.Value.MasterKey);
            Console.Out.WriteLine(base64);
        }
    }
}
