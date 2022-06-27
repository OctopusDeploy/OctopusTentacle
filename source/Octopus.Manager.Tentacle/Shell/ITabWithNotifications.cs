using System;

namespace Octopus.Manager.Tentacle.Shell
{
    public interface ITabWithNotifications
    {
        bool HasNotifications { get; }
        event EventHandler HasNotificationsChanged;
    }
}