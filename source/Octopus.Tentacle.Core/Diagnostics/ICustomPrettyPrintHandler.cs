using System;
using System.Text;

namespace Octopus.Tentacle.Core.Diagnostics
{
    /// <summary>
    /// BEWARE your custom exception types must be public or the dynamic dispatch we do in here will not work correctly!!!
    /// </summary>
    /// <typeparam name="TException"></typeparam>
    public interface ICustomPrettyPrintHandler<in TException>
        where TException : Exception
    {
        /// <summary>
        /// Custom handler for PrettyPrinting a type of exception
        /// </summary>
        /// <param name="sb">StringBuilder for the "pretty" output</param>
        /// <param name="ex">The exception instance</param>
        /// <returns>True if the processing should continue on to processing stack trace or inner exceptions</returns>
        bool Handle(StringBuilder sb, TException ex);
    }
}