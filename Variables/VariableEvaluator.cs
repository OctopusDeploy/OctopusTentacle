using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Octopus.Platform.Variables.Templates.Analyzer;
using Octopus.Platform.Variables.Templates.Binder;
using Octopus.Platform.Variables.Templates.Evaluator;
using Octopus.Platform.Variables.Templates.Parser;
using Octopus.Platform.Variables.Templates.Parser.Ast;

namespace Octopus.Platform.Variables
{
    /// <summary>
    /// This class once served as the bridge between the "v1" regex-based
    /// variable substitution and the "v2" Template substitutions. It carries
    /// a lot of design baggage because of that. 
    /// </summary>
    public static class VariableEvaluator
    {
        public static bool HasReferences(string value)
        {
            Template result;
            string parserError;
            return TemplateParser.TryParseTemplate(value, out result, out parserError) &&
                (result.Tokens.Length > 1 ||
                 result.Tokens.Length == 1 && !(result.Tokens[0] is TextToken));
        }

        public static void Evaluate(VariableDictionary variables)
        {
            var nodes = variables.Select(variable => new VariableNode(variable)).ToList();

            var config = GetConfiguration(variables);

            ParseDependencies(nodes, config);

            EnsureThereAreNoCycles(nodes);

            PerformSubstitution(nodes);
        }

        static Configuration GetConfiguration(VariableDictionary variables)
        {
            var config = new Configuration();
            config.IgnoreMissingTokens = variables.GetFlag(Constants.IgnoreMissingVariableTokens, true);
            return config;
        }

        static void EnsureThereAreNoCycles(IEnumerable<VariableNode> nodes)
        {
            foreach (var node in nodes)
            {
                node.CheckForCycles();
            }
        }

        static void ParseDependencies(List<VariableNode> nodes, Configuration config)
        {
            foreach (var node in nodes)
            {
                GetExtendedDependencies(nodes, config, node);
            }
        }

        static void GetExtendedDependencies(List<VariableNode> nodes, Configuration config, VariableNode node)
        {
            node.ClearDependencies();

            var dependencies = node.ExtendedDependencyNames
                .Where(n => !Constants.IsBuiltInName(n));

            foreach (var name in dependencies)
            {
                if (name.Contains("*"))
                {
                    var parts = name.Split(new[] {'*'}, StringSplitOptions.RemoveEmptyEntries);
                    var toEnd = name.EndsWith("*");
                    foreach (var dependency in nodes.Where(n => IsCollectionDependency(parts, n.Name, toEnd)))
                    {
                        node.AddDependency(dependency);
                    }
                }
                else
                {
                    var dependency = nodes.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.InvariantCultureIgnoreCase));
                    if (dependency == null)
                    {
                        if (!config.IgnoreMissingTokens)
                        {
                            throw new Exception(string.Format("The variable '{0}' contains a reference to another variable named '{1}', which is not defined. Set the special variable {2} to have Tentacle print the value of all variables to help diagnose this problem.", node.Name, name, Constants.PrintVariables));
                        }
                    }
                    else
                    {
                        node.AddDependency(dependency);
                    }
                }
            }
        }

        static bool IsCollectionDependency(IEnumerable<string> pathSegments, string variableName, bool toEnd)
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

        static void PerformSubstitution(IEnumerable<VariableNode> nodes)
        {
            foreach (var node in nodes)
            {
                node.CalculateAndStoreValue();
            }
        }

        class Configuration
        {
            public bool IgnoreMissingTokens;
        }

        class VariableNode
        {
            private readonly Variable variable;
            private readonly HashSet<VariableNode> dependencies = new HashSet<VariableNode>();
            private readonly Template template;

            public VariableNode(Variable variable)
            {
                this.variable = variable;
                string parserError; 
                TemplateParser.TryParseTemplate(Value, out template, out parserError);
            }

            public string Name { get { return variable.Name; } }

            string Value { get { return variable.Value ?? string.Empty; } }

            public IEnumerable<string> ExtendedDependencyNames
            {
                get
                {
                    if (template == null) return Enumerable.Empty<string>();

                    return TemplateAnalyzer.GetDependencies(template);
                }
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

            public void CalculateAndStoreValue()
            {
                // Opportunity to output the parser error here if/when we have
                // a "debug mode" - this behavior will output the raw value.

                if (template == null)
                    return;

                foreach (var dependency in dependencies)
                    dependency.CalculateAndStoreValue();

                variable.IsSensitive |= dependencies.Any(d => d.variable.IsSensitive);

                var properties = dependencies.ToDictionary(d => d.Name, d => d.Value);
                var binding = PropertyListBinder.CreateFrom(properties);
                var result = new StringWriter();
                TemplateEvaluator.Evaluate(template, binding, result);
                variable.Value = result.ToString();
            }

            public void ClearDependencies()
            {
                dependencies.Clear();
            }
        }
    }
}