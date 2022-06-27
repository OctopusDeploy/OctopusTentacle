using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace Octopus.Tentacle.Configuration.Proxy
{
    
    public interface IProxyPasswordMaskValuesProvider
    {
        IEnumerable<string> GetProxyPasswordMaskValues(string proxyPassword);
    }
    
    public class ProxyPasswordMaskValuesProvider : IProxyPasswordMaskValuesProvider
    {
        static readonly Regex UrlEncodedCharactersRegex = new Regex(@"%[A-F0-9]{2}", RegexOptions.Compiled);
        public IEnumerable<string> GetProxyPasswordMaskValues(string proxyPassword)
        {
            if (string.IsNullOrEmpty(proxyPassword)) 
                return Enumerable.Empty<string>();

            //$Env:HTTP_PROXY will contain the URL encoded version of the password
            //We also need to handle cases where the encoded hex is in upper or lower case
            //Calamari calls both WebUtility.UrlEncode (uppercase hex) and HttpUtility.UrlEncode (lowercase hex)
            string upperCaseUrlEncodedProxyPassword = WebUtility.UrlEncode(proxyPassword);
            string lowerCaseUrlEncodedProxyPassword = UrlEncodedCharactersRegex.Replace(upperCaseUrlEncodedProxyPassword, m => m.Value.ToLowerInvariant());
            
            return new[]
            {
                proxyPassword,
                upperCaseUrlEncodedProxyPassword,
                lowerCaseUrlEncodedProxyPassword
            }.Distinct();
        }
    }
}