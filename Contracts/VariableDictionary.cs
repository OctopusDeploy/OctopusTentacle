using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Octopus.Shared.Contracts
{
    public class VariableDictionary
    {
        readonly List<Variable> variables = new List<Variable>();

        public VariableDictionary(IEnumerable<Variable> variables)
        {
            if (variables == null)
            {
                return;
            }

            foreach (var variable in variables)
            {
                if (variable == null)
                    continue;

                Set(variable.Name, variable.Value);
            }
        }

        public void Set(string name, string value)
        {
            var existing = Get(name);
            if (existing == null)
            {
                variables.Add(new Variable(name, value));
            }
            else
            {
                existing.Value = value;
            }
        }

        /// <summary>
        /// Performs a case-insensitive lookup of a variable by name, returning null if the variable is not defined.
        /// </summary>
        /// <param name="variableName">Name of the variable.</param>
        /// <returns>The value of the variable, or null if one is not defined.</returns>
        public Variable Get(string variableName)
        {
            return variables.LastOrDefault(x => string.Equals(x.Name, variableName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Performs a case-insensitive lookup of a variable by name, returning null if the variable is not defined.
        /// </summary>
        /// <param name="variableName">Name of the variable.</param>
        /// <returns>The value of the variable, or null if one is not defined.</returns>
        public string GetValue(string variableName)
        {
            var variable = Get(variableName);
            return variable != null ? variable.Value : null;
        }

        public bool GetFlag(string variableName, bool defaultValueIfUnset)
        {
            bool value;
            var text = GetValue(variableName);
            if (string.IsNullOrWhiteSpace(text) || !bool.TryParse(text, out value))
            {
                value = defaultValueIfUnset;
            }
            
            return value;
        }

        public int? GetInt32(string variableName)
        {
            int value;
            var text = GetValue(variableName);
            if (string.IsNullOrWhiteSpace(text) || !int.TryParse(text, out value))
            {
                return null;
            }

            return value;
        }

        public ReadOnlyCollection<Variable> AsList()
        {
            return variables.AsReadOnly();
        }

        public IDictionary<string, string> AsDictionary()
        {
            var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in variables)
            {
                dictionary[item.Name] = item.Value;
            }

            return dictionary;
        }
    }
}