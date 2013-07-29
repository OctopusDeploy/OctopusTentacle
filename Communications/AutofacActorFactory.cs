using System;
using Autofac.Features.Indexed;
using Autofac.Features.OwnedInstances;
using Pipefish;
using Pipefish.WellKnown.Dispatch;

namespace Octopus.Shared.Communications
{
    public class AutofacActorFactory : IActorFactory
    {
        readonly IIndex<string, Func<Owned<IActor>>> actorsCreatedByMessageType;

        public AutofacActorFactory(IIndex<string, Func<Owned<IActor>>> actorsCreatedByMessageType)
        {
            if (actorsCreatedByMessageType == null) throw new ArgumentNullException("actorsCreatedByMessageType");
            this.actorsCreatedByMessageType = actorsCreatedByMessageType;
        }

        public IActor CreateActorFor(string messageType)
        {
            // Disposal needs to be accounted for here
            return actorsCreatedByMessageType[messageType].Invoke().Value;
        }
    }
}