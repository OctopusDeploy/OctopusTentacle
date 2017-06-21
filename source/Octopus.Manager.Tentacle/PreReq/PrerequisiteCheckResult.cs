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

        public static PrerequisiteCheckResult Successful()
        {
            var result = new PrerequisiteCheckResult();
            result.Success = true;
            return result;
        }

        public static PrerequisiteCheckResult Failed(string message, string commandLineSolution = null, string helpLink = null, string helpLinkText = null)
        {
            var result = new PrerequisiteCheckResult();
            result.Success = false;
            result.Message = message;
            result.CommandLineSolution = commandLineSolution;
            result.HelpLink = helpLink;
            result.HelpLinkText = helpLinkText;
            return result;
        }
    }
}