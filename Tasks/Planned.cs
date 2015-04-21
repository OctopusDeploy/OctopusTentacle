using System;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Logging;

namespace Octopus.Shared.Tasks
{
    public static class Planned
    {
        public static Planned<T> Create<T>(T item, string name)
        {
            return Create(item, name, null);
        }

        public static Planned<T> CreateUnplannedGroup<T>(T item, string name, LogCorrelator logCorrelator)
        {
            return Create(item, name, logCorrelator);
        }

        public static Planned<T> CreateUnplanned<T>(T item, string name)
        {
            return Create(item, name, CreateUnplanned(name));
        }

        public static Planned<T> Create<T>(T item, string name, LogCorrelator logCorrelator)
        {
            return new Planned<T>(item, name, logCorrelator);
        }

        static LogCorrelator CreateUnplanned(string name)
        {
            using (Log.Octopus().OpenBlock(name))
            {
                return Log.Octopus().Current;
            }
        }
    }

    public class Planned<T>
    {
        readonly T workItem;
        readonly string name;
        readonly LogCorrelator logCorrelator;

        public Planned(T workItem, string name, LogCorrelator logCorrelator)
        {
            this.workItem = workItem;
            this.name = name;
            this.logCorrelator = logCorrelator ?? Log.Octopus().PlanFutureBlock(name);
        }

        public T WorkItem
        {
            get { return workItem; }
        }

        public string Name
        {
            get { return name; }
        }

        public LogCorrelator LogCorrelator
        {
            get { return logCorrelator; }
        }
    }
}