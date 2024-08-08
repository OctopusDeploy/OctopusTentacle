using System.Reflection;
using System.Runtime.Versioning;
using System.Windows;

[assembly: AssemblyTitle("Octopus.Manager.Tentacle")]
[assembly: ThemeInfo(
    ResourceDictionaryLocation.None, //where theme specific resource dictionaries are located
                                     //(used if a resource is not found in the page, 
                                     // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly //where the generic resource dictionary is located
                                              //(used if a resource is not found in the page, 
                                              // app, or any theme specific resource dictionaries)
)]

#if NET8_0_OR_GREATER
[assembly: SupportedOSPlatform("windows")]
#endif