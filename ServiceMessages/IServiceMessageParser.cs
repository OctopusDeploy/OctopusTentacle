using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Octopus.Shared.ServiceMessages
{
    public interface IServiceMessageParser
    {
        /// <summary>
        /// Lazy parses service messages from string
        /// </summary>
        /// <param name="text">text to parse</param>
        /// <returns>enumerable of service messages</returns>
        ParseServiceMessageResult ParseServiceMessages(string text);
    }
}