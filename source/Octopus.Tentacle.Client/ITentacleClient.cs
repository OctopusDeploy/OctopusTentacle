using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Octopus.Diagnostics;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;

namespace Octopus.Tentacle.Client
{
    public interface ITentacleClient : IDisposable
    {
        Task<UploadResult> UploadFile(string fileName, string path, DataStream package, ILog logger, CancellationToken cancellationToken);
        Task<DataStream?> DownloadFile(string remotePath, ILog logger, CancellationToken cancellationToken);

        /// <summary>
        /// Execute a script on Tentacle
        /// </summary>
        /// <param name="startScriptCommand">The start script command</param>
        /// <param name="onScriptStatusResponseReceived"></param>
        /// <param name="onScriptCompleted">Called when the script has finished executing on Tentacle but before the working directory is cleaned up.
        /// This is called regardless of the outcome of the script. It will also be called if the script execution is cancelled.</param>
        /// <param name="logger">Used to output user orientated log messages</param>
        /// <param name="scriptExecutionCancellationToken">When cancelled, will attempt to stop the execution of the script on Tentacle before returning.</param>
        /// <returns></returns>
        Task<ScriptExecutionResult> ExecuteScript(StartScriptCommandV3Alpha startScriptCommand,
            OnScriptStatusResponseReceived onScriptStatusResponseReceived,
            OnScriptCompleted onScriptCompleted,
            ILog logger,
            CancellationToken scriptExecutionCancellationToken);
    }
}