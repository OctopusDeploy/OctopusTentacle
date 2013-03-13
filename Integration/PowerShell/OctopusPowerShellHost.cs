using System;
using System.Globalization;
using System.Management.Automation.Host;
using System.Threading;

namespace Octopus.Shared.Integration.PowerShell
{
    public class OctopusPowerShellHost : PSHost
    {
        readonly PSHostUserInterface ui;
        Guid instanceId = Guid.Empty;

        public OctopusPowerShellHost(PSHostUserInterface ui)
        {
            this.ui = ui;
        }

        public override CultureInfo CurrentCulture
        {
            get { return Thread.CurrentThread.CurrentCulture; }
        }

        public override CultureInfo CurrentUICulture
        {
            get { return Thread.CurrentThread.CurrentUICulture; }
        }

        public override void EnterNestedPrompt()
        {
        }

        public override void ExitNestedPrompt()
        {
        }

        public override Guid InstanceId
        {
            get
            {
                if (instanceId == Guid.Empty)
                {
                    instanceId = Guid.NewGuid();
                }

                return instanceId;
            }
        }

        public override string Name
        {
            get { return typeof (OctopusPowerShellHost).Name; }
        }

        public override void NotifyBeginApplication()
        {
        }

        public override void NotifyEndApplication()
        {
        }

        public override void SetShouldExit(int exitCode)
        {
            ExitCode = exitCode;
        }

        public int ExitCode { get; set; }

        public override PSHostUserInterface UI
        {
            get { return ui; }
        }

        public override Version Version
        {
            get { return typeof (OctopusPowerShellHost).Assembly.GetName().Version; }
        }
    }
}