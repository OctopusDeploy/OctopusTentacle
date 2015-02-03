using System;

namespace Octopus.Shared.Contracts
{
    public class TaskTicket : IEquatable<TaskTicket>
    {
        public TaskTicket(string taskId)
        {
            if (taskId == null) throw new ArgumentNullException("taskId");
            TaskId = taskId;
        }

        public string TaskId { get; set; }

        public bool Equals(TaskTicket other)
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
            return Equals((TaskTicket) obj);
        }

        public override int GetHashCode()
        {
            return (TaskId != null ? TaskId.GetHashCode() : 0);
        }

        public static bool operator ==(TaskTicket left, TaskTicket right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(TaskTicket left, TaskTicket right)
        {
            return !Equals(left, right);
        }

        public override string ToString()
        {
            return TaskId;
        }
    }
}