using System;
using System.Collections.Generic;
using System.Threading;

namespace Octopus.Tentacle.Contracts
{
    public class ScriptTicket : IEquatable<ScriptTicket>
    {
        static long nextTaskId;

        public ScriptTicket(string taskId, IReadOnlyList<string> supportedFeatures)
        {
            TaskId = taskId ?? throw new ArgumentNullException("taskId");
            SupportedFeatures = supportedFeatures;
        }

        public string TaskId { get; }

        public IReadOnlyList<string> SupportedFeatures { get; }

        public bool Equals(ScriptTicket? other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return string.Equals(TaskId, other.TaskId);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != GetType())
                return false;
            return Equals((ScriptTicket)obj);
        }

        public override int GetHashCode()
            => TaskId != null ? TaskId.GetHashCode() : 0;

        public static bool operator ==(ScriptTicket left, ScriptTicket right)
            => Equals(left, right);

        public static bool operator !=(ScriptTicket left, ScriptTicket right)
            => !Equals(left, right);

        public override string ToString()
            => TaskId;

        public static ScriptTicket Create(string? serverTaskId, IReadOnlyList<string> supportedFeatures)
        {
            serverTaskId = serverTaskId?.Replace("ServerTasks-", string.Empty);
            return new ScriptTicket($"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{serverTaskId}-{Interlocked.Increment(ref nextTaskId)}", supportedFeatures);
        }
    }
}