using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Tests.Integration.TentacleClient
{
    public class TentacleClient
    {
        public TentacleClient(IScriptService scriptService, IFileTransferService fileTransferService)
        {
            ScriptService = scriptService;
            FileTransferService = fileTransferService;
        }

        public IScriptService ScriptService { get; }
        public IFileTransferService FileTransferService { get; }
    }
}