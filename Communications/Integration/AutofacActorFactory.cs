using System;
using Autofac.Features.Indexed;
using Autofac.Features.OwnedInstances;
using Pipefish.Core;
using Pipefish.WellKnown.Dispatch;

namespace Octopus.Shared.Communications.Integration
{
    public class AutofacActorFactory : IActorFactory
    {
        readonly IIndex<string, Func<Owned<IActor>>> actorsCreatedByMessageType;
        readonly string subsetSuffix;

        public AutofacActorFactory(IIndex<string, Func<Owned<IActor>>> actorsCreatedByMessageType, string subsetSuffix = null)
        {
            if (actorsCreatedByMessageType == null) throw new ArgumentNullException("actorsCreatedByMessageType");
            this.actorsCreatedByMessageType = actorsCreatedByMessageType;
            this.subsetSuffix = subsetSuffix;
        }

        public IActor CreateActorFor(string messageType)
        {
            Func<Owned<IActor>> factory;
            if (subsetSuffix == null || !actorsCreatedByMessageType.TryGetValue(messageType + "+" + subsetSuffix, out factory))
            {
                if (!actorsCreatedByMessageType.TryGetValue(messageType, out factory))
                    throw new ArgumentException("No actor is registered for creation on " + messageType);
            }
            
            // Disposal needs to be accounted for here
            return factory.Invoke().Value;
        }
    }
}