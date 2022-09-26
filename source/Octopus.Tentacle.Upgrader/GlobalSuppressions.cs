// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System;
using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Usage", "PC001:API not supported on all platforms", Justification = "Upgrader only support windows anyway", Scope = "member", Target = "~M:Octopus.Tentacle.Upgrader.Program.PerformUpgrade(System.String[])~System.Int32")]
[assembly: SuppressMessage("Usage", "PC001:API not supported on all platforms", Justification = "Upgrader only support windows anyway", Scope = "member", Target = "~M:Octopus.Tentacle.Upgrader.ServiceBouncer.GetRegistryValue(System.String,System.String)~System.String")]
[assembly: SuppressMessage("Usage", "PC001:API not supported on all platforms", Justification = "Upgrader only support windows anyway", Scope = "member", Target = "~M:Octopus.Tentacle.Upgrader.SoftwareInstaller.IsPendingServerRestart()~System.Boolean")]