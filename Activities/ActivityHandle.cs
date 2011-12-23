using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Octopus.Shared.Activities
{
    public class ActivityHandle : IActivityHandle
    {
        readonly List<ActivityHandle> children = new List<ActivityHandle>();
        readonly ManualResetEvent runningHandle = new ManualResetEvent(false);
        readonly ManualResetEvent completeHandle = new ManualResetEvent(false);

        public ActivityHandle(string name)
        {
            Name = name;
            Status = ActivityStatus.Pending;
            Log = new StringBuilder();
        }

        public string Name { get; private set; }
        public ActivityStatus Status { get; private set; }
        public Exception Error { get; private set; }
        public StringBuilder Log { get; private set; }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void AddChild(ActivityHandle handle)
        {
            children.Add(handle);
        }

        public void ChangeStatus(ActivityStatus status, Exception error = null)
        {
            Status = status;
            Error = error;

            switch (status)
            {
                case ActivityStatus.Pending:
                    break;
                case ActivityStatus.Running:
                    runningHandle.Set();
                    break;
                case ActivityStatus.Success:
                case ActivityStatus.Failed:
                    runningHandle.Set();
                    completeHandle.Set();
                    break;
                default:
                    throw new ArgumentOutOfRangeException("status");
            }
        }

        public bool IsComplete
        {
            get { return Status == ActivityStatus.Failed || Status == ActivityStatus.Success; }
        }

        public void WaitForRunning()
        {
            runningHandle.WaitOne();
        }

        public void WaitForComplete()
        {
            completeHandle.WaitOne();
        }

        ReadOnlyCollection<ActivityHandle> IActivityHandle.Children
        {
            get { return children.AsReadOnly(); }
        }
    }

    public interface IActivityHandle
    {
        string Name { get; }
        ActivityStatus Status { get; }
        Exception Error { get; }
        StringBuilder Log { get; }
        ReadOnlyCollection<ActivityHandle> Children { get; }
        bool IsComplete { get; }

        void WaitForRunning();
        void WaitForComplete();
    }


}