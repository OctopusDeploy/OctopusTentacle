namespace Octopus.Tentacle.Contracts
{
    public static class PowerShellStartupDetectionTemplateValues
    {
        /// <summary>
        /// Tentacle will use this to prevent the script from running in some cases.
        /// If this is used, it MUST be above anything else run in the PowerShell script.
        /// Otherwise, Tentacle may falsely assume the script has not started.
        /// </summary>
        public const string PowershellStartupDetectionCommentMustBeAtTheStartOfTheScript = "# TENTACLE-POWERSHELL-STARTUP-DETECTION-AND-GUARD-MUST-BE-AT-THE-START-OF-THE-SCRIPT";
    }
}
