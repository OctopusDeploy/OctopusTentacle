using System;
using System.Collections.Generic;

namespace Octopus.Shared.Activities
{
    public class ActivityResolver : IActivityResolver
    {
        private readonly Dictionary<Type, Func<object>> factories = new Dictionary<Type,Func<object>>();

        public void Register<TArguments>(Func<IActivity<TArguments>> activityFactory) where TArguments : IActivityMessage
        {
            factories.Add(typeof(TArguments), activityFactory);
        }

        public object Locate(IActivityMessage message)
        {
            return factories[message.GetType()]();
        }
    }
}