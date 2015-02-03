using System;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Logging;

namespace Octopus.Shared.Tasks
{
    public static class Planned
    {
        public static Planned<T> Create<T>(T item, string name)
        {
            var logCorrelator = Log.Octopus().PlanFutureBlock(name);
            return Create(item, name, logCorrelator);
        }

        public static Planned<T> Create<T>(T item, string name, LogCorrelator logCorrelator)
        {
            return new Planned<T>(item, name, logCorrelator);
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
            this.logCorrelator = logCorrelator;
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