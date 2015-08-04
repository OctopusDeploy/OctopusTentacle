using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Shared.Pipeline
{
    /// <summary>
    /// A pipeline of processing components that run in parallel communicating
    /// between each other using a small buffer.
    /// </summary>
    /// <remarks>
    /// The pipeline typically starts with with an IEnumerable. This is consumed on
    /// one thread and typically reads from a file or network source spending most of
    /// its time blocked waiting for I/O. Additional processing stages can be added
    /// after that doing CPU intensive work (without limiting the rate at which the
    /// reader thread can do I/O). Finally the results can be consumed as an IEnumerable
    /// back on the calling thread where typically you do another I/O bound operation writing
    /// to a file or database.
    /// 
    /// Overall this approach ensures that one I/O operation (reader or writer) is never 
    /// waiting needlessly for another I/O operation (writer or reader) and that no I/O
    /// operation is ever waiting for a CPU intensive task.
    /// 
    /// Overall the pipeline thus runs at the speed of its slowest component and not at the
    /// sum of the times of each component.   
    /// </remarks>
    public class Pipeline
    {
        protected readonly Pipeline Previous;
        protected readonly TaskFactory TaskFactory;
        protected readonly CancellationTokenSource CancellationTokenSource;
        protected Task Task;

        protected Pipeline(TaskFactory taskFactory, CancellationTokenSource cancellationTokenSource)
        {
            TaskFactory = taskFactory;
            CancellationTokenSource = cancellationTokenSource;
            Previous = null;
        }

        protected Pipeline(Pipeline previous)
        {
            this.Previous = previous;
            this.TaskFactory = previous.TaskFactory;
            this.CancellationTokenSource = previous.CancellationTokenSource;
        }

        public static Pipeline<T> Start<T>(IPipelineProducer<T> producer, int bufferSize, CancellationToken cancellationToken)
        {
            return new Pipeline<T>(producer, bufferSize, CancellationTokenSource.CreateLinkedTokenSource(cancellationToken));
        }

        public static Pipeline<T> Start<T>(IEnumerable<T> enumerable, int bufferSize, CancellationToken cancellationToken)
        {
            return new Pipeline<T>(enumerable, bufferSize, CancellationTokenSource.CreateLinkedTokenSource(cancellationToken));
        }

        protected IEnumerable<Task> AllTasks()
        {
            var builder = this;
            int safety = 1000;
            while (builder != null)
            {
                if (builder.Task != null)
                {
                    yield return builder.Task;
                }
                builder = builder.Previous;
                if (safety-- == 0) throw new Exception("AllTasks got stuck");
            }
        }
    }

    public class Pipeline<T> : Pipeline, IEnumerable<T>
    {
        readonly BlockingCollection<T> buffer;
        public Exception Exception { get; set; }

        /// <summary>
        /// Start a new pipeline
        /// </summary>
        internal Pipeline(IPipelineProducer<T> producer, int bufferSize, CancellationTokenSource cancellationTokenSource)
            : base(new TaskFactory(TaskCreationOptions.LongRunning, TaskContinuationOptions.LongRunning), cancellationTokenSource)
        {
            buffer = new BlockingCollection<T>(bufferSize);
            Task = producer.Start(TaskFactory, buffer, cancellationTokenSource.Token);
        }

        /// <summary>
        /// Start a new pipeline from an Enumerable
        /// </summary>
        internal Pipeline(IEnumerable<T> enumerable, int bufferSize, CancellationTokenSource cancellationTokenSource)
            : base(new TaskFactory(TaskCreationOptions.LongRunning, TaskContinuationOptions.LongRunning), cancellationTokenSource)
        {
            var cancellationToken = CancellationTokenSource.Token;
            buffer = new BlockingCollection<T>(bufferSize);
            Task = TaskFactory.StartNew(() =>
            {
                try
                {
                    foreach (var item in enumerable)
                    {
                        buffer.Add(item, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception)
                {
                    cancellationTokenSource.Cancel();
                    throw;
                }
                finally
                {
                    buffer.CompleteAdding();
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);
        }

        private Pipeline(Pipeline previous, BlockingCollection<T> buffer, Task nextTask)
            : base(previous)
        {
            this.buffer = buffer;
            this.Task = nextTask;
        }

        /// <summary>
        /// Add a processing step to the pipeline using a consumer/producer
        /// which, unlike the Func overload can be staeful
        /// </summary>
        public Pipeline<U> Then<U>(IPipelineConsumerProducer<T, U> step, int bufferSize)
        {
            var outputBuffer = new BlockingCollection<U>();
            var nextTask = step.Start(TaskFactory, buffer, outputBuffer, CancellationTokenSource.Token);
            return new Pipeline<U>(this, outputBuffer, nextTask);
        }

        /// <summary>
        /// A functional step converting from one type to another
        /// </summary>
        /// <remarks>
        /// Use this only if the function is sufficiently CPU intensive to warrant its own step
        /// otherwise better off putting this transform in the Start or Sink
        /// </remarks>
        public Pipeline<U> Then<U>(Func<T, U> step, int bufferSize)
        {
            var cancellationToken = CancellationTokenSource.Token;
            var outputBuffer = new BlockingCollection<U>();
            var nextTask = TaskFactory.StartNew(() =>
            {
                try
                {
                    foreach (var source in buffer.GetConsumingEnumerable(cancellationToken))
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        var converted = step(source);
                        outputBuffer.Add(converted, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception)
                {
                    // Shut down the whole pipeline if any stage fails
                    CancellationTokenSource.Cancel();
                    throw;
                }
                finally
                {
                    outputBuffer.CompleteAdding();
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);
            return new Pipeline<U>(this, outputBuffer, nextTask);
        }

        /// <summary>
        /// Sink the final step in a pipeline and wait until the pipeline completes
        /// </summary>
        public void Sink(IPipelineConsumer<T> sink)
        {
            var finalTask = sink.Start(TaskFactory, buffer, CancellationTokenSource.Token);
            Task.WaitAll(AllTasks().ToArray());
            finalTask.Wait();
        }

        /// <summary>
        /// Get final items and process on calling thread rather than a separate thread
        /// </summary>
        public IEnumerator<T> GetEnumerator()
        {
            return new PipelineSink(this, buffer, CancellationTokenSource.Token);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private class PipelineSink : IEnumerator<T>
        {
            readonly Pipeline<T> pipeline;
            readonly IEnumerator<T> enumerator; 

            public PipelineSink(Pipeline<T> pipeline, BlockingCollection<T> source, CancellationToken cancellationToken)
            {
                this.pipeline = pipeline;
                this.enumerator = source.GetConsumingEnumerable(cancellationToken).GetEnumerator();
            }

            public T Current
            {
                get { return enumerator.Current; }
            }

            public void Dispose()
            {
                enumerator.Dispose();
            }

            object System.Collections.IEnumerator.Current
            {
                get { return this.Current; }
            }

            public bool MoveNext()
            {
                try
                {
                    if (enumerator.MoveNext()) return true;
                }
                catch (OperationCanceledException)
                {
                }

                // wait for pipeline to shutdown or throw any exceptions from earlier stages
                var allTasks = pipeline.AllTasks().ToArray();
                Task.WaitAll(allTasks);
                return false;
            }

            public void Reset()
            {
                enumerator.Reset();
            }
        }

        /// <summary>
        /// Segments the stream and returns each segment as it own Pipeline
        /// </summary>
        public Pipeline<Pipeline<T>> SegmentParallel<U>(Func<T, U> segmenter, int outerBufferSize, int innerBufferSize) where U:IEquatable<U>
        {
            var cancellationToken = CancellationTokenSource.Token;
            var outputBuffer = new BlockingCollection<Pipeline<T>>(outerBufferSize);
            var nextTask = TaskFactory.StartNew(() =>
            {
                var currentSegment = default(U);
                BlockingCollection<T> currentCollection = null;

                try
                {
                    foreach (var source in buffer.GetConsumingEnumerable(cancellationToken))
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        if (source == null) continue;

                        var segment = segmenter(source);
                        if (currentCollection == null || !currentSegment.Equals(segment))
                        {
                            // Starting a new segment
                            if (currentCollection != null)
                            {
                                currentCollection.CompleteAdding();
                            }
                            currentSegment = segment;
                            currentCollection = new BlockingCollection<T>(innerBufferSize);
                            var innerPipe = new Pipeline<T>(this, currentCollection, null);
                            outputBuffer.Add(innerPipe, cancellationToken);
                        }
                        currentCollection.Add(source, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception)
                {
                    // Shut down the whole pipeline if any stage fails
                    CancellationTokenSource.Cancel();
                    throw;
                }
                finally
                {
                    if (currentCollection != null)
                    {
                        currentCollection.CompleteAdding();
                    }
                    outputBuffer.CompleteAdding();
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);
            return new Pipeline<Pipeline<T>>(this, outputBuffer, nextTask);
        }
    }
}