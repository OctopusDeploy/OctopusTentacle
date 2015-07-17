using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Shared.Pipeline
{
    public interface IPipelineConsumerProducer<T, U>
    {
        Task Start(TaskFactory taskFactory, BlockingCollection<T> inputbuffer, BlockingCollection<U> outputbuffer, CancellationToken cancellationToken);
    }
}
