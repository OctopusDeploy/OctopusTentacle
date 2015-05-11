using System;

namespace Octopus.Shared.Tasks
{
    [AttributeUsage(AttributeTargets.Class)]
    public class TaskControllerAttribute : Attribute
    {
        readonly string taskName;
        readonly Type argumentsType;

        public TaskControllerAttribute(string taskName, Type argumentsType)
        {
            this.taskName = taskName;
            this.argumentsType = argumentsType;
        }

        public string TaskName
        {
            get { return taskName; }
        }

        public Type ArgumentsType
        {
            get { return argumentsType; }
        }
    }
}