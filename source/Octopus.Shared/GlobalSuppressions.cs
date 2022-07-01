// This file is used by Code Analysis to maintain SuppressMessage 
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given 
// a specific target and scoped to a namespace, type, member, etc.

using System;
using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Usage",
    "DE0003:API is deprecated",
    Justification = "<Pending>",
    Scope = "member",
    Target = "~F:Octopus.Shared.Configuration.ProxyConfigParser.GetSystemWebProxy")]
[assembly: SuppressMessage("Usage",
    "DE0003:API is deprecated",
    Justification = "<Pending>",
    Scope = "member",
    Target = "~M:Octopus.Shared.Configuration.ProxyInitializer.InitializeProxy")]
[assembly: SuppressMessage("Usage",
    "DE0003:API is deprecated",
    Justification = "<Pending>",
    Scope = "member",
    Target = "~M:Octopus.Shared.Configuration.ProxyInitializer.GetProxy~System.Net.IWebProxy")]
[assembly: SuppressMessage("Usage",
    "PC001:API not supported on all platforms",
    Justification = "<Pending>",
    Scope = "member",
    Target = "~M:Octopus.Shared.Configuration.RegistryApplicationInstanceStore.GetListFromRegistry(Octopus.Shared.Configuration.ApplicationName)~System.Collections.Generic.IEnumerable{Octopus.Shared.Configuration.ApplicationInstanceRecord}")]
[assembly: SuppressMessage("Usage",
    "PC001:API not supported on all platforms",
    Justification = "<Pending>",
    Scope = "member",
    Target = "~M:Octopus.Shared.Configuration.RegistryApplicationInstanceStore.DeleteFromRegistry(Octopus.Shared.Configuration.ApplicationName,System.String)")]
[assembly: SuppressMessage("Usage",
    "PC001:API not supported on all platforms",
    Justification = "<Pending>",
    Scope = "member",
    Target = "~M:Octopus.Shared.Security.CertificateGenerator.Generate(System.String,System.Boolean,Octopus.Diagnostics.ILog)~System.Security.Cryptography.X509Certificates.X509Certificate2")]
[assembly: SuppressMessage("Usage",
    "PC001:API not supported on all platforms",
    Justification = "<Pending>",
    Scope = "type",
    Target = "Octopus.Shared.Security.Certificates.CertificateEncoder")]