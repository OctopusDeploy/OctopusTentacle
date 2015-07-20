using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Shared.Pipeline
{
    public interface IPipelineConsumer<T> : IPipelineComponent
    {
        Task Start(TaskFactory taskFactory, BlockingCollection<T> buffer, CancellationToken cancellationToken);
    }
}
