using System;
using System.Collections.Generic;
using Octopus.Shared.Configuration;

namespace Octopus.Shared.Startup
{
    public class AbstractMultiOperationCommand : AbstractStandardCommand
    {
        readonly List<Action> operations = new List<Action>();

        protected AbstractMultiOperationCommand(IApplicationInstanceSelector instanceSelector) : base(instanceSelector)
        {
        }

        protected override void Start()
        {
            foreach (var operation in operations) operation();
        }

        protected void QueueOperation(Action action)
        {
            operations.Add(action);
        }
    }
}