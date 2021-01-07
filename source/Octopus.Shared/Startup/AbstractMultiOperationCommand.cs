using System;
using System.Collections.Generic;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration.Instances;

namespace Octopus.Shared.Startup
{
    public class AbstractMultiOperationCommand : AbstractStandardCommand
    {
        readonly List<Action> operations = new List<Action>();

        protected AbstractMultiOperationCommand(IApplicationInstanceSelector instanceSelector, ISystemLog log) : base(instanceSelector, log)
        {
        }

        protected override void Start()
        {
            base.Start();
            foreach (var operation in operations) operation();
        }

        protected void QueueOperation(Action action)
        {
            operations.Add(action);
        }
    }
}