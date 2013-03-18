using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Security;
using System.Text;

namespace Octopus.Shared.Integration.Scripting.PowerShellLegacy
{
    public class OctopusPowerShellHostUserInterface : PSHostUserInterface
    {
        readonly ScriptOutput log;
        readonly StringBuilder buffer = new StringBuilder();
        readonly OctopusPowerShellHostRawUserInterface rawUi = new OctopusPowerShellHostRawUserInterface();
        bool hasErrors;

        public OctopusPowerShellHostUserInterface(ScriptOutput log)
        {
            this.log = log;
        }

        public bool HasErrors
        {
            get { return hasErrors; }
        }

        public override Dictionary<string, PSObject> Prompt(string caption, string message, Collection<FieldDescription> descriptions)
        {
            throw new NotSupportedException("Prompt is not supported by the Octopus PowerShell host");
        }

        public override int PromptForChoice(string caption, string message, Collection<ChoiceDescription> choices, int defaultChoice)
        {
            return defaultChoice;
        }

        public override PSCredential PromptForCredential(string caption, string message, string userName, string targetName, PSCredentialTypes allowedCredentialTypes, PSCredentialUIOptions options)
        {
            throw new NotSupportedException("PromptForCredential is not supported by the Octopus PowerShell host");
        }

        public override PSCredential PromptForCredential(string caption, string message, string userName, string targetName)
        {
            throw new NotSupportedException("PromptForCredential is not supported by the Octopus PowerShell host");
        }

        public override PSHostRawUserInterface RawUI
        {
            get { return rawUi; }
        }

        public override string ReadLine()
        {
            throw new NotSupportedException("ReadLine is not supported by the Octopus PowerShell host");
        }

        public override SecureString ReadLineAsSecureString()
        {
            throw new NotSupportedException("ReadLineAsSecureString is not supported by the Octopus PowerShell host");
        }

        public override void Write(ConsoleColor foregroundColor, ConsoleColor backgroundColor, string value)
        {
            Write(value);
        }

        public override void Write(string value)
        {
            var lines = value.Split('\n');
            if (lines.Length > 1)
            {
                var tail = lines.Last();
                var others = lines.Take(lines.Length - 1);
                foreach (var line in others)
                {
                    WriteLine(line);
                }

                buffer.Append(tail);
            }
            else
            {
                buffer.Append(value);
            }
        }

        public override void WriteDebugLine(string message)
        {
            WritePrefixLine(message, "DEBUG");
        }

        public override void WriteErrorLine(string value)
        {
            hasErrors = true;
            WritePrefixLine(value, "ERROR");
        }

        void WritePrefixLine(string value, string prefix)
        {
            WriteLine(prefix + ": " + value);
        }

        public override void WriteLine(string value)
        {
            buffer.Append(value);
            log.OnWritten(buffer.ToString());
            buffer.Length = 0;
        }

        public override void WriteProgress(long sourceId, ProgressRecord record)
        {
        }

        public override void WriteVerboseLine(string message)
        {
            WritePrefixLine(message, "VERBOSE");
        }

        public override void WriteWarningLine(string message)
        {
            WritePrefixLine(message, "WARNING");
        }
    }
}