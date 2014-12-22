using System;

namespace Octopus.Platform.Security.Permissions
{
    public class AuthorizationResult
    {
        readonly bool isAuthorized;
        readonly Lazy<string> helpText;

        public AuthorizationResult(bool isAuthorized, Lazy<string> helpText = null)
        {
            this.isAuthorized = isAuthorized;
            this.helpText = helpText ?? new Lazy<string>(() => null);
        }

        public bool IsAuthorized
        {
            get { return isAuthorized; }
        }

        public string HelpText
        {
            get { return helpText.Value; }
        }
    }
}