using System;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Tasks
{
    public class OctoThreadClosure<T>
    {
        readonly ILogWithContext log = Log.Octopus();
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
            try
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
                        }
                        else if (ex is ControlledFailureException)
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
            catch (Exception ex)
            {
                // Something must have gone wrong finishing or disposing the log context
                // If we let the exception propogate, it will tear down the process

                if (Exception == null)
                    Exception = ex;
            }
        }
    }
}