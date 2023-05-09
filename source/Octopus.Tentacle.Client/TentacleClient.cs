using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Tests.Integration.TentacleClient
{
    public class TentacleClient
    {
        public TentacleClient(IScriptService scriptService, IFileTransferService fileTransferService, IScriptServiceV2 scriptServiceV2)
        {
            ScriptService = scriptService;
            FileTransferService = fileTransferService;
            ScriptServiceV2 = scriptServiceV2;
        }

        public IScriptService ScriptService { get; }
        public IFileTransferService FileTransferService { get; }
        public IScriptServiceV2 ScriptServiceV2 { get; }
    }
}