using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Octopus.Diagnostics;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Client
{
    public interface ITentacleClient : IDisposable
    {
        Task<UploadResult> UploadFile(string fileName, string path, DataStream package, ILog logger, CancellationToken cancellationToken);
        Task<DataStream?> DownloadFile(string remotePath, ILog logger, CancellationToken cancellationToken);

        Task<ScriptExecutionResult> ExecuteScript(
            StartScriptCommandV2 startScriptCommand,
            Action<ScriptStatusResponseV2> onScriptStatusResponseReceived,
            Func<CancellationToken, Task> onScriptCompleted,
            ILog logger,
            CancellationToken cancellationToken);
    }
}