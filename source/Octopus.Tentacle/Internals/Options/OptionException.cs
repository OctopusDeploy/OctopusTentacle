using System;
using System.Runtime.Serialization;

namespace Octopus.Tentacle.Internals.Options
{
    [Serializable]
    public class OptionException : Exception
    {
        public OptionException(string message, string? optionName)
            : base(message)
        {
            OptionName = optionName;
        }

        public OptionException(string message, string? optionName, Exception innerException)
            : base(message, innerException)
        {
            OptionName = optionName;
        }

#if NET8_0_OR_GREATER
        [Obsolete("Formatter-based serialization is obsolete and should not be used", DiagnosticId = "SYSLIB0051")]
#else
        [Obsolete("Formatter-based serialization is obsolete and should not be used")]
#endif
        protected OptionException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            OptionName = info.GetString("OptionName");
        }

        public string? OptionName { get; }

#if NET8_0_OR_GREATER
        [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
#endif
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("OptionName", OptionName);
        }
    }
}