using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Octopus.Shared.Variables.Templates;

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

        public Variable FindRaw(string variableName)
        {
            Variable variable;
            if (variables.TryGetValue(variableName, out variable) && variable != null)
                return variable;

            return null;
        }

        public Variable Find(string variableName)
        {
            var variable = FindRaw(variableName);

            return EvaluateVariable(variable);
        }

        /// <summary>
        /// Performs a case-insensitive lookup of a variable by name, returning null if the variable is not defined.
        /// </summary>
        /// <param name="variableName">Name of the variable.</param>
        /// <returns>The value of the variable, or null if one is not defined.</returns>
        public string GetRaw(string variableName)
        {
            Variable variable;
            if (variables.TryGetValue(variableName, out variable) && variable != null)
                return variable.Value;

            return null;
        }

        public string Get(string variableName)
        {
            Variable variable;
            if (!variables.TryGetValue(variableName, out variable) || variable == null)
                return null;

            return EvaluateVariable(variable).Value;
        }

        Variable EvaluateVariable(Variable variable)
        {
            if (variable == null) return null;

            var node = new VariableNode(variable);
            var ignoreMissingTokens = GetFlag(Constants.IgnoreMissingVariableTokens, true);

            node.ParseDependencies(variables, ignoreMissingTokens);

            node.CheckForCycles();
            
            return node.Evaluate();
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
            var text = GetRaw(variableName);
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
    class VariableNode
    {
        readonly Variable variable;
        readonly HashSet<VariableNode> dependencies = new HashSet<VariableNode>();
        readonly Template template;

        public VariableNode(Variable variable)
        {
            this.variable = variable;
            string parserError;
            TemplateParser.TryParseTemplate(Value, out template, out parserError);
        }

        public string Name { get { return variable.Name; } }

        public string Value { get { return variable.Value ?? string.Empty; } }

        public IEnumerable<string> ExtendedDependencyNames
        {
            get
            {
                if (template == null) return Enumerable.Empty<string>();

                return TemplateAnalyzer.GetDependencies(template);
            }
        }

        public void ParseDependencies(IDictionary<string, Variable> variables, bool ignoreMissingTokens)
        {
            ClearDependencies();

            var dependencyNames = ExtendedDependencyNames
                .Where(n => !Constants.IsBuiltInName(n));

            foreach (var name in dependencyNames)
            {
                if (name.Contains("*"))
                {
                    var parts = name.Split(new[] { '*' }, StringSplitOptions.RemoveEmptyEntries);
                    var toEnd = name.EndsWith("*");
                    foreach (var dependency in variables.Where(v => IsCollectionDependency(parts, v.Key, toEnd)))
                    {
                        AddDependency(new VariableNode(dependency.Value));
                    }
                }
                else
                {
                    var dependency = variables.FirstOrDefault(x => string.Equals(x.Key, name, StringComparison.InvariantCultureIgnoreCase)).Value;
                    if (dependency == null)
                    {
                        if (!ignoreMissingTokens)
                        {
                            throw new Exception(string.Format("The variable '{0}' contains a reference to another variable named '{1}', which is not defined. Set the special variable {2} to have Tentacle print the value of all variables to help diagnose this problem.", Name, name, Constants.PrintVariables));
                        }
                    }
                    else
                    {
                        AddDependency(new VariableNode(dependency));
                    }
                }
            }
        }

        bool IsCollectionDependency(IEnumerable<string> pathSegments, string variableName, bool toEnd)
        {
            // This is still not strictly correct, as [indexers] are correctly allowed as wildcards,
            // but so are .Dotted.Path.Segments.
            // Fixing this is not so hard, just requires parsing the variable
            // name with TemplateParser.ParseIdentifierPath() and matching on that, but
            // leaving it for now as when we make the switch to templates everywhere, different (better)
            // implementation options should be available.

            var lastIndex = -1;
            foreach (var pathSegment in pathSegments)
            {
                var ix = variableName.IndexOf(pathSegment, StringComparison.OrdinalIgnoreCase);
                if (ix < 0)
                    return false;
                if (lastIndex == -1 && ix != 0)
                    return false;
                if (ix <= lastIndex)
                    return false;
                lastIndex = ix;
            }
            return !toEnd || variableName.EndsWith(pathSegments.Last());
        }

        public void AddDependency(VariableNode dependency)
        {
            dependencies.Add(dependency);
        }

        public void CheckForCycles()
        {
            var seen = new Stack<VariableNode>();
            seen.Push(this);

            CheckForCycles(dependencies, seen);
        }

        void CheckForCycles(IEnumerable<VariableNode> list, Stack<VariableNode> seenBefore)
        {
            foreach (var dependency in list)
            {
                if (dependency == this)
                {
                    var reason = string.Join(" -> ", seenBefore.Select(x => x.Name).ToArray().Reverse()) + " -> " + Name;
                    throw new Exception(string.Format("Variables could not be evaluated because the variable '{0}' is cyclic: {1}", Name, reason));
                }

                if (seenBefore.Contains(dependency))
                    return;

                seenBefore.Push(dependency);

                CheckForCycles(dependency.dependencies, seenBefore);

                seenBefore.Pop();
            }
        }

        public Variable Evaluate()
        {
            // Opportunity to output the parser error here if/when we have
            // a "debug mode" - this behavior will output the raw value.

            if (template == null)
                return variable;

            foreach (var dependency in dependencies)
                dependency.Evaluate();

            variable.IsSensitive |= dependencies.Any(d => d.variable.IsSensitive);

            var properties = dependencies.ToDictionary(d => d.Name, d => d.Value);
            var binding = PropertyListBinder.CreateFrom(properties);
            var result = new StringWriter();
            TemplateEvaluator.Evaluate(template, binding, result);
            variable.Value = result.ToString();
            return variable;
        }

        public void ClearDependencies()
        {
            dependencies.Clear();
        }
    }
}