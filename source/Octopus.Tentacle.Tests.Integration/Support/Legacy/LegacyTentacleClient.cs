namespace Octopus.Tentacle.Tests.Integration.Support.Legacy
{
    public class LegacyTentacleClient
    {
        public LegacyTentacleClient(
            SyncAndAsyncScriptServiceV1 scriptService, 
            SyncAndAsyncFileTransferServiceV1 fileTransferService, 
            SyncAndAsyncCapabilitiesServiceV2 capabilitiesServiceV2)
        {
            ScriptService = scriptService;
            FileTransferService = fileTransferService;
            CapabilitiesServiceV2 = capabilitiesServiceV2;
        }

        public SyncAndAsyncScriptServiceV1 ScriptService { get; }
        public SyncAndAsyncFileTransferServiceV1 FileTransferService { get; }
        public SyncAndAsyncCapabilitiesServiceV2 CapabilitiesServiceV2 { get; }
    }
}