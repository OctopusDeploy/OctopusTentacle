using System;
using System.Net;

namespace Octopus.Core.Extensions
{
    public static class HttpResponseExtensions
    {
        /// <summary>
        /// Test to see if a response was successful
        /// </summary>
        /// <returns>true if the response was successful, and false otherwise</returns>
        public static bool IsSuccessStatusCode(this HttpWebResponse response)
        {
            return (int) response.StatusCode >= 200 && (int) response.StatusCode <= 299;
        }
    }
}