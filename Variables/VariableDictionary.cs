using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Shared.Variables
{
    // TODO: This shouldn't need Variable objects anymore, it should be a simple string key/string value store
    public class VariableDictionary : IEnumerable<Variable>
    {
        readonly IDictionary<string, Variable> variables = new Dictionary<string, Variable>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the <see cref="VariableDictionary"/> class.
        /// </summary>
        public VariableDictionary() : this((IEnumerable<Variable>)null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VariableDictionary"/> class.
        /// </summary>
        /// <param name="variables"></param>
        public VariableDictionary(IEnumerable<KeyValuePair<string, string>> variables)
            : this(variables.Select(kv => new Variable(kv.Key, kv.Value)))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VariableDictionary"/> class.
        /// </summary>
        /// <param name="variables">The variables.</param>
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

                Set(variable.Name, variable.Value, variable.IsSensitive);
            }
        }

        public void SetFlag(string name, bool value = true)
        {
            Set(name, value.ToString());
        }

        public void Set(string name, string value, bool isSensitive = false)
        {
            if (name == null) return;

            Variable item;
            if (!variables.TryGetValue(name, out item))
            {
                variables.Add(name, new Variable(name, value, isSensitive));
            }
            else
            {
                item.Value = value;
                item.IsSensitive = isSensitive;
            }
        }

        public void SetStrings(string variableName, IEnumerable<string> values, string separator = ",", bool isSensitive = false)
        {
            var value = string.Join(separator, values.Where(v => !string.IsNullOrWhiteSpace(v)));
            Set(variableName, value, isSensitive);
        }

        public void SetPaths(string variableName, IEnumerable<string> values, bool isSensitive = false)
        {
            SetStrings(variableName, values, Environment.NewLine, isSensitive);
        }

        public Variable Find(string variableName)
        {
            Variable variable;
            if (variables.TryGetValue(variableName, out variable) && variable != null)
                return variable;

            return null;
        }

        /// <summary>
        /// Performs a case-insensitive lookup of a variable by name, returning null if the variable is not defined.
        /// </summary>
        /// <param name="variableName">Name of the variable.</param>
        /// <returns>The value of the variable, or null if one is not defined.</returns>
        public string Get(string variableName)
        {
            Variable variable;
            if (variables.TryGetValue(variableName, out variable) && variable != null)
                return variable.Value;

            return null;
        }

        public IEnumerable<string> GetStrings(string variableName, params char[] separators)
        {
            separators = separators ?? new char[0];
            if (separators.Length == 0) separators = new[] { ',' };

            var value = Get(variableName);
            if (string.IsNullOrWhiteSpace(value))
                return Enumerable.Empty<string>();

            var values = value.Split(separators)
                .Select(v => v.Trim())
                .Where(v => v != "");

            return values.ToArray();
        }

        public IEnumerable<string> GetPaths(string variableName)
        {
            return GetStrings(variableName, '\r', '\n');
        }

        public bool GetFlag(string variableName, bool defaultValueIfUnset = false)
        {
            bool value;
            var text = Get(variableName);
            if (string.IsNullOrWhiteSpace(text) || !bool.TryParse(text, out value))
            {
                value = defaultValueIfUnset;
            }
            
            return value;
        }

        public int? GetInt32(string variableName)
        {
            int value;
            var text = Get(variableName);
            if (string.IsNullOrWhiteSpace(text) || !int.TryParse(text, out value))
            {
                return null;
            }

            return value;
        }

        public string Require(string name)
        {
            if (name == null) throw new ArgumentNullException("name");
            var value = Get(name);
            if (string.IsNullOrEmpty(value))
                throw new ArgumentOutOfRangeException("name", "The variable '" + name + "' is required but no value is set.");
            return value;
        }

        public void Add(string name, string value)
        {
            Set(name, value);
        }

        public IEnumerator<Variable> GetEnumerator()
        {
            return variables.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void UpdateFrom(IEnumerable<Variable> updated)
        {
            foreach (var variable in updated)
            {
                variables[variable.Name] = variable;
            }
        }

        public bool TryGetValue(string name, out Variable item)
        {
            return variables.TryGetValue(name, out item);
        }
    }
}