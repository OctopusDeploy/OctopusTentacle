using System;

namespace Octopus.Shared.Tasks
{
    public delegate void TaskCompletionHandler(string taskId, Exception error);
}