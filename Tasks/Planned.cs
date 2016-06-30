using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Tasks
{
    public static class Planned
    {
        public static Planned<T> Create<T>(T item, string name)
        {
            return Create(item, name, null);
        }

        public static Planned<T> CreateUnplannedGroup<T>(T item, string name, LogContext logContext)
        {
            return Create(item, name, logContext);
        }

        public static Planned<T> CreateUnplanned<T>(T item, string name)
        {
            return Create(item, name, CreateUnplanned(name));
        }

        public static Planned<T> Create<T>(T item, string name, LogContext logContext)
        {
            return new Planned<T>(item, name, logContext);
        }

        static LogContext CreateUnplanned(string name)
        {
            using (Log.Octopus().OpenBlock(name))
            {
                return Log.Octopus().CurrentContext;
            }
        }
    }

    public class Planned<T>
    {
        readonly T workItem;
        readonly string name;
        readonly LogContext logContext;

        public Planned(T workItem, string name, LogContext logContext)
        {
            this.workItem = workItem;
            this.name = name;
            this.logContext = logContext ?? Log.Octopus().PlanFutureBlock(name);
        }

        public T WorkItem
        {
            get { return workItem; }
        }

        public string Name
        {
            get { return name; }
        }

        public LogContext LogContext
        {
            get { return logContext; }
        }
    }
}