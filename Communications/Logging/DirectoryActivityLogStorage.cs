using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Pipefish;
using Pipefish.Standard;

namespace Octopus.Shared.Communications.Logging
{
    public class DirectoryActivityLogStorage : IActivityLogStorage
    {
        private readonly string rootDirectory;

        public DirectoryActivityLogStorage(string rootDirectory)
        {
            this.rootDirectory = rootDirectory;

            if (!Directory.Exists(rootDirectory)) Directory.CreateDirectory(rootDirectory);
        }

        public async Task AppendAsync(LogMessage logMessage)
        {
            var filePath = GetLogFilePath(logMessage.CorrelationId);

            using (var file = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            using (var writer = new StreamWriter(file))
            {
                var text = JsonConvert.ToString(logMessage.MessageText);

                await writer.WriteLineAsync(logMessage.CorrelationId + "|" + logMessage.Occurred + "|" + logMessage.Category + "|" + logMessage.GetMessage().From + "|" + text);

                await writer.FlushAsync();
            }
        }

        public async Task<IList<ActivityLogTreeNode>> GetLogAsync(string correlationId)
        {
            var filePath = GetLogFilePath(correlationId);
            if (!File.Exists(filePath))
                return new List<ActivityLogTreeNode>();

            var correlationIdToNode = new Dictionary<string, NodeBuilder>();

            using (var file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(file))
            {
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    ParseLine(line, correlationIdToNode);
                }
            }

            return correlationIdToNode.Where(n => n.Value.Parent == null).Select(n => n.Value.ToTreeNode()).ToList();
        }

        static void ParseLine(string line, IDictionary<string, NodeBuilder> correlationIdToNode)
        {
            var parts = line.Split('|');
            var correlationId = parts[0];
            var occurred = DateTimeOffset.Parse(parts[1]);
            var category = (ActivityLogCategory)Enum.Parse(typeof (ActivityLogCategory), parts[2]);
            var actorId = new ActorId(parts[3]);
            var message = JsonConvert.DeserializeObject<string>(parts[4]);

            var node = GetNode(correlationId, correlationIdToNode);
            node.LogEntries.Add(new ActivityLogEntry(occurred, category, actorId, message));
        }

        static NodeBuilder GetNode(string correlationId, IDictionary<string, NodeBuilder> nodes)
        {
            NodeBuilder node;

            if (nodes.TryGetValue(correlationId, out node)) 
                return node;

            node = new NodeBuilder(correlationId);
            nodes.Add(correlationId, node);

            var parentIndex = correlationId.LastIndexOf('/');
            if (parentIndex < 0)
                return node;

            var parentCorrelationId = correlationId.Substring(0, parentIndex);
            var parentNode = GetNode(parentCorrelationId, nodes);
            parentNode.Children.Add(node);
            node.Parent = parentNode;

            return node;
        }

        string GetLogFilePath(string correlationId)
        {
            var firstCorrelationId = correlationId.Split('/').FirstOrDefault() ?? correlationId;
            var filePath = Path.Combine(rootDirectory, "ActivityLog_" + firstCorrelationId + ".txt");
            return filePath;
        }

        class NodeBuilder
        {
            readonly string correlationId;

            public NodeBuilder(string correlationId)
            {
                this.correlationId = correlationId;
                LogEntries = new List<ActivityLogEntry>();
                Children = new List<NodeBuilder>();
            }

            public List<ActivityLogEntry> LogEntries { get; private set; }
            public List<NodeBuilder> Children { get; private set; }
            public NodeBuilder Parent { get; set; }

            public ActivityLogTreeNode ToTreeNode()
            {
                return new ActivityLogTreeNode(correlationId, LogEntries, Children.Select(c => c.ToTreeNode()));
            }
        }
    }
}