using System;
using System.Collections.Generic;
using System.Text;
using Octopus.Shared.Diagnostics;
using log4net;

namespace Octopus.Shared.Integration.Transforms
{
    /// <summary>
    /// Parse string of parameters 
    /// </summary>
    public static class ParametersParser
    {
        static readonly ILog Log = Logger.Default;

        /// <summary>
        /// Parse string of parameters <paramref name="parametersString"/> separated by semi ';'.
        /// Value should be separated from name by colon ':'. 
        /// If value has spaces or semi you can use quotes for value. 
        /// You can escape symbols '\' and '"' with \.
        /// </summary>
        /// <param name="parametersString">String of parameters</param>
        /// <param name="parameters">All parameters will be read to current dictionary.</param>
        public static void ReadParameters(string parametersString, IDictionary<string, string> parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException("parameters");

            if (string.IsNullOrWhiteSpace(parametersString))
                return;

            var source = parametersString.ToCharArray();

            var index = 0;

            var fParameterNameRead = true;
            var fForceParameterValueRead = false;

            var parameterName = new StringBuilder();
            var parameterValue = new StringBuilder();

            while (index < source.Length)
            {
                if (fParameterNameRead && source[index] == ':')
                {
                    fParameterNameRead = false;
                    index++;

                    if (index < source.Length && source[index] == '"')
                    {
                        fForceParameterValueRead = true;
                        index++;
                    }

                    continue;
                }

                if ((!fForceParameterValueRead && source[index] == ';')
                    || (fForceParameterValueRead && source[index] == '"' && ((index + 1) == source.Length || source[index + 1] == ';')))
                {
                    AddParameter(parameters, parameterName, parameterValue);
                    index++;
                    if (fForceParameterValueRead)
                        index++;
                    parameterName.Clear();
                    parameterValue.Clear();
                    fParameterNameRead = true;
                    fForceParameterValueRead = false;
                    continue;
                }

                // Check is this escape \{ \} \\
                if (source[index] == '\\')
                {
                    var nextIndex = index + 1;
                    if (nextIndex < source.Length)
                    {
                        var nextChar = source[nextIndex];
                        if (nextChar == '"' || nextChar == '\\')
                        {
                            index++;
                        }
                    }
                }

                if (fParameterNameRead)
                {
                    parameterName.Append(source[index]);
                }
                else
                {
                    parameterValue.Append(source[index]);
                }

                index++;
            }

            AddParameter(parameters, parameterName, parameterValue);

            if (Log.IsDebugEnabled)
            {
                foreach (var parameter in parameters)
                {
                    Log.DebugFormat("Parameter Name: '{0}', Value: '{1}'", parameter.Key, parameter.Value);
                }
            }
        }

        static void AddParameter(IDictionary<string, string> parameters, StringBuilder parameterName, StringBuilder parameterValue)
        {
            var name = parameterName.ToString();
            if (!string.IsNullOrWhiteSpace(name))
            {
                if (parameters.ContainsKey(name))
                    parameters.Remove(name);
                parameters.Add(name, parameterValue.ToString());
            }
        }
    }
}