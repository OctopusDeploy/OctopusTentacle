using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Octopus.Shared.Communications.Logging
{
    public class ActivityLogTreeNode
    {
        readonly string correlationId;
        readonly ReadOnlyCollection<ActivityLogEntry> logEntries;
        readonly ReadOnlyCollection<ActivityLogTreeNode> children;

        public ActivityLogTreeNode(string correlationId, IEnumerable<ActivityLogEntry> logEntries, IEnumerable<ActivityLogTreeNode> children)
        {
            this.correlationId = correlationId;
            this.logEntries = new List<ActivityLogEntry>(logEntries).AsReadOnly();
            this.children = new List<ActivityLogTreeNode>(children).AsReadOnly();
        }

        public string CorrelationId
        {
            get { return correlationId; }
        }

        public ReadOnlyCollection<ActivityLogTreeNode> Children
        {
            get { return children; }
        }

        public ReadOnlyCollection<ActivityLogEntry> LogEntries
        {
            get { return logEntries; }
        }
    }
}