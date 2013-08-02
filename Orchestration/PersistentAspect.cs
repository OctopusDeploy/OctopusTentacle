using System;
using System.Collections.Generic;
using Pipefish.Core;
using Pipefish.Persistence;
using Pipefish.Standard;

namespace Octopus.Shared.Orchestration
{
    public abstract class PersistentAspect<TAspectData> : Aspect
        where TAspectData : class
    {
        readonly string stateKey;

        protected TAspectData AspectData { get; set; }

        protected PersistentAspect(string stateKey)
        {
            this.stateKey = stateKey;
        }

        public override void Attach(IActor actor, IActivitySpace space)
        {
            base.Attach(actor, space);

            var persistent = actor as IPersistentActor;
            if (persistent == null) return;

            persistent.AfterLoading += () => LoadAspectState(persistent.State);
            persistent.BeforeSaving += () => SaveAspectState(persistent.State);
        }

        void LoadAspectState(IDictionary<string, object> state)
        {
            object savedState;
            if (state.TryGetValue(stateKey, out savedState))
            {
                AspectData = (TAspectData)savedState;
            }
        }

        void SaveAspectState(IDictionary<string, object> state)
        {
            if (AspectData != null)
                state[stateKey] = AspectData;
        }
    }
}
