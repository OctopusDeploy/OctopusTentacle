using System;
using System.Threading;

namespace Octopus.Shared.Contracts
{
    public class ScriptTicket : IEquatable<ScriptTicket>
    {
        static long nextTaskId = 0;

        public ScriptTicket(string taskId)
        {
            if (taskId == null) throw new ArgumentNullException("taskId");
            TaskId = taskId;
        }

        public string TaskId { get; set; }

        public bool Equals(ScriptTicket other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return string.Equals(TaskId, other.TaskId);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            if (obj.GetType() != this.GetType())
            {
                return false;
            }
            return Equals((ScriptTicket) obj);
        }

        public override int GetHashCode()
        {
            return (TaskId != null ? TaskId.GetHashCode() : 0);
        }

        public static bool operator ==(ScriptTicket left, ScriptTicket right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ScriptTicket left, ScriptTicket right)
        {
            return !Equals(left, right);
        }

        public override string ToString()
        {
            return TaskId;
        }

        public static ScriptTicket Create()
        {
            return new ScriptTicket("Script-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Interlocked.Increment(ref nextTaskId));
        }
    }
}