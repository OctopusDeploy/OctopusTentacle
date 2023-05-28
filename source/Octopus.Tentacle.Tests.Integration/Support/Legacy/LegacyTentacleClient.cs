using System;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;

namespace Octopus.Tentacle.Tests.Integration.Support.Legacy
{
    public class LegacyTentacleClient
    {
        public LegacyTentacleClient(IScriptService scriptService, IFileTransferService fileTransferService, ICapabilitiesServiceV2 capabilitiesServiceV2)
        {
            ScriptService = scriptService;
            FileTransferService = fileTransferService;
            CapabilitiesServiceV2 = capabilitiesServiceV2;
        }

        public IScriptService ScriptService { get; }
        public IFileTransferService FileTransferService { get; }
        public ICapabilitiesServiceV2 CapabilitiesServiceV2 { get; }
    }
}