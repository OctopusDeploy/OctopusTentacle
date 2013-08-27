using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using System.Runtime.Remoting;
using Octopus.Platform.Util;

namespace Octopus.Shared.Integration.Scripting.PowerShellLegacy
{
    public class HostedPowerShellRunner : IScriptRunner
    {
        public string[] GetSupportedExtensions()
        {
            return new[] { "ps1" };
        }

        public ScriptExecutionResult Execute(ScriptArguments arguments)
        {
            var setup = new AppDomainSetup();
            setup.ApplicationBase = Path.GetDirectoryName(typeof(HostedPowerShellRunner).Assembly.FullLocalPath());

            var appDomain = AppDomain.CreateDomain("PowerShellInvocation" + Guid.NewGuid(), AppDomain.CurrentDomain.Evidence, setup);
            appDomain.Load(typeof(HostedPowerShellRunner).Assembly.GetName());
            appDomain.Load(typeof(PSHost).Assembly.GetName());

            try
            {
                var executor = (IsolatedScriptRunner)appDomain.CreateInstanceAndUnwrap(typeof(IsolatedScriptRunner).Assembly.FullName, typeof(IsolatedScriptRunner).FullName);
                var result = executor.Execute(arguments);
                return result;
            }
            finally
            {
                RemotingServices.Disconnect(arguments.OutputStream);
                RemotingServices.Disconnect(arguments);

                AppDomain.Unload(appDomain);
            }
        }

        public class IsolatedScriptRunner : RemotedObject
        {
            public ScriptExecutionResult Execute(ScriptArguments arguments)
            {
                var oldWorkingDirectory = Environment.CurrentDirectory;
                Environment.CurrentDirectory = arguments.WorkingDirectory;

                var ui = new OctopusPowerShellHostUserInterface(arguments.OutputStream);
                var host = new OctopusPowerShellHost(ui);

                var outputVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                try
                {
                    using (var space = RunspaceFactory.CreateRunspace(host))
                    {
                        space.Open();

                        foreach (var variable in arguments.Variables)
                        {
                            // This is the way we used to fix up the identifiers - people might still rely on this behavior
                            var legacyKey = new string(variable.Key.Where(char.IsLetterOrDigit).ToArray());
                            space.SessionStateProxy.SetVariable(legacyKey, variable.Value);

                            // This is the way we should have done it
                            var smartKey = new string(variable.Key.Where(IsValidPowerShellIdentifierChar).ToArray());
                            space.SessionStateProxy.SetVariable(smartKey, variable.Value);
                        }

                        space.SessionStateProxy.SetVariable("OctopusParameters", arguments.Variables);
                        space.SessionStateProxy.SetVariable("OctopusOutputParameters", outputVariables);

                        var script = new Command(string.Format("& '{0}'", arguments.ScriptFilePath.Replace("'", "''")), true);
                        script.MergeMyResults(PipelineResultTypes.Error, PipelineResultTypes.Output);

                        using (var executePipeline = space.CreatePipeline())
                        {
                            executePipeline.Commands.AddScript("try { Set-ExecutionPolicy Bypass -Scope Process } catch { Write-Host \"Unable to modify the execution policy.\"  } ");
                            executePipeline.Commands.AddScript("function Set-OctopusVariable([string]$name, [string]$value) { $OctopusOutputParameters[$name] = $value; }");
                            executePipeline.Commands.Add(script);
                            executePipeline.Commands.Add("Out-Default");
                            executePipeline.Commands.AddScript(@"Get-Job | Remove-Job -Force");
                            executePipeline.Invoke();

                            var lastExit = space.SessionStateProxy.GetVariable("LASTEXITCODE");
                            if (host.ExitCode == 0 && lastExit != null)
                            {
                                host.ExitCode = (int)lastExit;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    arguments.OutputStream.OnWritten("Unhandled error: " + ex);
                    host.ExitCode = -8;
                }
                finally
                {
                    Environment.CurrentDirectory = oldWorkingDirectory;
                }

                if (host.ExitCode == 0 && ui.HasErrors)
                {
                    host.ExitCode = -12;
                }

                var result = new ScriptExecutionResult(host.ExitCode, ui.HasErrors, outputVariables);
                return result;
            }

            static bool IsValidPowerShellIdentifierChar(char c)
            {
                return c == '_' || char.IsLetterOrDigit(c);
            }
        }
    }
}