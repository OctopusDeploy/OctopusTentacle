using System;

namespace Octopus.Shared.Configuration.Instances
{
    /// <summary>
    /// An Instance is one way of setting up one or more Octopus Server or Tentacle installations on a computer. The list of instances is stored somewhere on the computer.
    /// Back in 2011, that was the Windows Registry. Fast-forward to 2016, we went cross-platform, so we moved everything to a well-known location on the file system.
    /// Fast-forward even further to 2020 and a world of containers, you don't really need multiple instances on the same computer, just run multiple containers configured
    /// by environment variables and secret volume mounts.
    /// 
    /// An instance store is nothing fancy, just list of names each with a pointer to a config file.
    /// 
    /// When Octopus Server or Tentacle is running, the process is considered to be running for an instance (multiple can actually run for an instance in a HA setup for
    /// Octopus Server). The CurrentConfiguration will get you to an IKeyValueStore that can always get values, whether from a config file or environment variables or wherever
    /// we need to in the future. The WritableCurrentConfiguration is can be used to write values when a configuration file can be found based on the command line parameters at startup.
    /// If no file is available the WriteableCurrentConfiguration will not save the values and provide details to help explain why.
    /// </summary>
    public interface IApplicationInstanceSelector
    {
        ApplicationName ApplicationName { get; }
        ApplicationInstanceConfiguration Current { get;  }
        bool CanLoadCurrentInstance();
    }
}