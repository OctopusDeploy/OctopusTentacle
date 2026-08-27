using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Logging;
using Octopus.Tentacle.Contracts.Observability;

namespace Octopus.Tentacle.Client
{
    public interface IRpcActions
    {
        Task<DataStream> DownloadFile(string remotePath, CancellationToken cancellationToken);
        Task<UploadResult> UploadFile(string path, DataStream package, CancellationToken cancellationToken);
        IScriptExecutor CreateScriptExecutor(ITentacleClientTaskLog logger,
            ITentacleClientObserver tentacleClientObserver,
            IClientOperationMetricsBuilder operationMetricsBuilder,
            TentacleClientOptions clientOptions,
            TimeSpan onCancellationAbandonCompleteScriptAfter );
    }
}