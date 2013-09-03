using System;
using Autofac.Core.Registration;
using Autofac.Features.Indexed;
using Autofac.Features.OwnedInstances;
using Pipefish.Core;
using Pipefish.WellKnown.Dispatch;

namespace Octopus.Shared.Communications.Integration
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
            try
            {
                // Disposal needs to be accounted for here
                return actorsCreatedByMessageType[messageType].Invoke().Value;
            }
            catch (ComponentNotRegisteredException cex)
            {                
                throw new ArgumentException("No actor is registered for creation on " + messageType, cex);
            }
        }
    }
}