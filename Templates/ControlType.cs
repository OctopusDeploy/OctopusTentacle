using System;
using System.Collections.Generic;

namespace Octopus.Shared.Templates
{
    public static class ControlType
    {
        public static readonly string ControlTypeKey = "Octopus.ControlType";

        public const string SingleLineText = "SingleLineText";
        public const string MultiLineText = "MultiLineText";
        public const string Select = "Select";
        public const string Checkbox = "Checkbox";
        public const string Sensitive = "Sensitive";
        public const string StepName = "StepName";
        public const string AzureAccount = "AzureAccount";
        public const string Certificate = "Certificate";

        public static Dictionary<string, string> AsDisplaySettings(string controlType)
        {
            return new Dictionary<string, string>()
            {
                {ControlTypeKey, controlType}
            };
        }
    }
}
