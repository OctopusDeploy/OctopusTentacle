using System;
using System.Runtime.Serialization;

namespace Octopus.Shared.Security.Masking
{
    public class MaskedException : Exception
    {
        const string DefaultMessage = "Sensitive data in the original exception has been masked";
        readonly string? originalException;

        public MaskedException(string originalException)
            : this(null, originalException)
        {
        }

        public MaskedException(string? message, string originalException)
            : base(message ?? DefaultMessage)
        {
            if (originalException == null) throw new ArgumentNullException("originalException");
            this.originalException = originalException;
        }

        public MaskedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        public override string ToString()
            => originalException ?? string.Empty;
    }
}