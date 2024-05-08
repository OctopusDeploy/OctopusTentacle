using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client
{
    public interface ITentacleClient : IDisposable
    {
        Task<UploadResult> UploadFile(string fileName, string path, DataStream package, ISomethingLog logger, CancellationToken cancellationToken);
        Task<DataStream?> DownloadFile(string remotePath, ISomethingLog logger, CancellationToken cancellationToken);

        /// <summary>
        /// Execute a script on Tentacle
        /// </summary>
        /// <param name="executeScriptCommand">The execute script command</param>
        /// <param name="onScriptStatusResponseReceived"></param>
        /// <param name="onScriptCompleted">Called when the script has finished executing on Tentacle but before the working directory is cleaned up.
        /// This is called regardless of the outcome of the script. It will also be called if the script execution is cancelled.</param>
        /// <param name="logger">Used to output user orientated log messages</param>
        /// <param name="scriptExecutionCancellationToken">When cancelled, will attempt to stop the execution of the script on Tentacle before returning.</param>
        /// <returns></returns>
        Task<ScriptExecutionResult> ExecuteScript(
            ExecuteScriptCommand executeScriptCommand,
            OnScriptStatusResponseReceived onScriptStatusResponseReceived,
            OnScriptCompleted onScriptCompleted,
            ISomethingLog logger,
            CancellationToken scriptExecutionCancellationToken);
    }
}