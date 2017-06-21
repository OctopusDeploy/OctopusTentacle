using System;
using System.Reflection;
using System.Threading;
using System.Windows;

namespace Octopus.Manager.Tentacle.Infrastructure
{
    public class DispatchHelper
    {
        public static void Foreground(Action callback)
        {
            Application.Current.Dispatcher.Invoke(callback);
        }

        public static void Background(Action callback)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    callback();
                }
                catch (Exception ex)
                {
                    Foreground(() => throw new TargetInvocationException(ex));
                }
            });
        }
    }
}