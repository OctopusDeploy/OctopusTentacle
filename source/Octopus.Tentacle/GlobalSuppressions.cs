// This file is used by Code Analysis to maintain SuppressMessage 
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given 
// a specific target and scoped to a namespace, type, member, etc.

using System;
using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Usage", "DE0003:API is deprecated",
    Justification = "<Pending>",
    Scope = "member",
    Target = "~M:Octopus.Tentacle.Communications.OctopusServerChecker.CheckServerCommunicationsIsOpen(System.Uri,System.Net.IWebProxy)~System.String")]
[assembly: SuppressMessage("Usage",
    "DE0003:API is deprecated",
    Justification = "<Pending>",
    Scope = "member",
    Target = "~F:Octopus.Tentacle.Configuration.ProxyConfigParser.GetSystemWebProxy")]
[assembly: SuppressMessage("Usage",
    "DE0003:API is deprecated",
    Justification = "<Pending>",
    Scope = "member",
    Target = "~M:Octopus.Tentacle.Configuration.ProxyInitializer.InitializeProxy")]
[assembly: SuppressMessage("Usage",
    "DE0003:API is deprecated",
    Justification = "<Pending>",
    Scope = "member",
    Target = "~M:Octopus.Tentacle.Configuration.ProxyInitializer.GetProxy~System.Net.IWebProxy")]
[assembly: SuppressMessage("Usage",
    "PC001:API not supported on all platforms",
    Justification = "<Pending>",
    Scope = "member",
    Target = "~M:Octopus.Tentacle.Configuration.RegistryApplicationInstanceStore.GetListFromRegistry()~System.Collections.Generic.IEnumerable{Octopus.Tentacle.Configuration.ApplicationInstanceRecord}")]
[assembly: SuppressMessage("Usage",
    "PC001:API not supported on all platforms",
    Justification = "<Pending>",
    Scope = "member",
    Target = "~M:Octopus.Tentacle.Configuration.RegistryApplicationInstanceStore.DeleteFromRegistry(Octopus.Tentacle.Configuration.ApplicationName,System.String)")]
[assembly: SuppressMessage("Usage",
    "PC001:API not supported on all platforms",
    Justification = "<Pending>",
    Scope = "member",
    Target = "~M:Octopus.Tentacle.Security.CertificateGenerator.Generate(System.String,System.Boolean,Octopus.Diagnostics.ILog)~System.Security.Cryptography.X509Certificates.X509Certificate2")]
[assembly: SuppressMessage("Usage",
    "PC001:API not supported on all platforms",
    Justification = "<Pending>",
    Scope = "type",
    Target = "Octopus.Tentacle.Security.Certificates.CertificateEncoder")]