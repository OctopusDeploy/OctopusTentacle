using System;
using System.Threading.Tasks;
using Halibut;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Tasks
{
    public class OctoThreadClosure<T>
    {
        readonly ILog log = Log.Octopus();
        readonly Planned<T> item;
        readonly Action<T> executeCallback;

        public OctoThreadClosure(Planned<T> item, Action<T> executeCallback)
        {
            this.item = item;
            this.executeCallback = executeCallback;
        }

        public Exception Exception { get; private set; }

        public void Execute()
        {
            using (log.WithinBlock(item.LogContext))
            {
                try
                {
                    executeCallback(item.WorkItem);
                }
                catch (Exception ex)
                {
                    if (ex is OperationCanceledException)
                    {
                        log.Info(ex.Message);
                    } else if (ex is ControlledFailureException)
                    {
                        log.Error(ex.Message);
                    }
                    else
                    {
                        log.Fatal(ex.Message);
                    }
                    Exception = ex;
                }
                finally
                {
                    log.Finish();
                }
            }
        }
    }
}