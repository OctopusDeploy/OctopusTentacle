namespace Octopus.Manager.Tentacle.PreReq
{
    public class PrerequisiteCheckResult
    {
        PrerequisiteCheckResult()
        {
        }

        public bool Success { get; private set; }
        public string Message { get; private set; }
        public string CommandLineSolution { get; private set; }
        public string HelpLink { get; private set; }
        public string HelpLinkText { get; private set; }
        public string CommandLineOutput { get; private set; }

        public static PrerequisiteCheckResult Successful()
        {
            var result = new PrerequisiteCheckResult();
            result.Success = true;
            return result;
        }

        public static PrerequisiteCheckResult Failed(string message, string commandLineSolution = null, string helpLink = null, string helpLinkText = null, string commandLineOutput = null)
        {
            var result = new PrerequisiteCheckResult
            {
                Success = false,
                Message = message,
                CommandLineSolution = commandLineSolution,
                HelpLink = helpLink,
                HelpLinkText = helpLinkText,
				CommandLineOutput = commandLineOutput
            };
            return result;
        }
    }
}