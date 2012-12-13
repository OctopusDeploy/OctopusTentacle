using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autofac;

namespace Octopus.Shared.Activities
{
    public class AutofacActivityResolver : IActivityResolver
    {
        readonly ILifetimeScope lifetimeScope;
        readonly Dictionary<Type, Type> messagesToActivities = new Dictionary<Type, Type>(); 

        public AutofacActivityResolver(ILifetimeScope lifetimeScope)
        {
            this.lifetimeScope = lifetimeScope;
        }

        public void Map<TMessage, TActivity>() where TMessage : ActivityMessage where TActivity : IActivity<TMessage>
        {
            Map(typeof(TMessage), typeof(TActivity));
        }

        public void Map(Type messageType, Type activityType)
        {
            var generic = typeof (IActivity<>).MakeGenericType(messageType);
            if (!generic.IsAssignableFrom(activityType))
            {
                throw new InvalidOperationException("The activity type: " + activityType + " should implement IActivity<" + messageType + ">");
            }

            messagesToActivities.Add(messageType, activityType);
        }

        public void Map(Assembly assembly)
        {
            var allTypes = assembly.GetTypes();

            var types =
                from type in allTypes
                where typeof (IActivityMessage).IsAssignableFrom(type) && type.IsNested
                let activity = type.DeclaringType
                select new {Activity = activity, Message = type};
            
            foreach (var type in types)
            {
                Map(type.Message, type.Activity);
            }
        }

        public object Locate(IActivityMessage message)
        {
            Type activityType;
            if (!messagesToActivities.TryGetValue(message.GetType(), out activityType))
            {
                throw new InvalidOperationException("Could not find an activity to process the message of type: " + message.GetType());
            }

            var activity = lifetimeScope.Resolve(activityType);
            return activity;
        }
    }
}