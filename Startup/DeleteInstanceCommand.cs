using System;
using Octopus.Platform.Diagnostics;
using Octopus.Platform.Util;
using Octopus.Shared.Configuration;

namespace Octopus.Shared.Startup
{
    public class DeleteInstanceCommand : AbstractCommand
    {
        readonly IOctopusFileSystem fileSystem;
        readonly IApplicationInstanceSelector instanceSelector;
        string instanceName;
        string config;

        public DeleteInstanceCommand(IOctopusFileSystem fileSystem, IApplicationInstanceSelector instanceSelector, ILog log)
        {
            this.fileSystem = fileSystem;
            this.instanceSelector = instanceSelector;
            Options.Add("instance=", "Name of the instance to delete", v => instanceName = v);
        }

        protected override void Start()
        {
            config = fileSystem.GetFullPath(config);

            if (string.IsNullOrWhiteSpace(instanceName))
            {
                instanceSelector.DeleteDefaultInstance();
            }
            else
            {
                instanceSelector.DeleteInstance(instanceName);
            }
        }
    }
}