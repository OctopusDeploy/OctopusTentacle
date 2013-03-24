using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Octopus.Shared.Contracts
{
    public class VariableEvaluator
    {
        private static readonly Regex DefaultTokenRegex = new Regex("\\#\\{(?<variable>.+?)\\}", RegexOptions.Compiled | RegexOptions.Singleline);

        public void Evaluate(VariableDictionary variables)
        {
            var nodes = variables.AsList().Select(variable => new VariableNode(variable)).ToList();

            var config = GetConfiguration(variables);

            ParseDependencies(nodes, config);

            EnsureThereAreNoCycles(nodes);

            PerformSubstitution(nodes, config);
        }

        static Configuration GetConfiguration(VariableDictionary variables)
        {
            var tokenRegex = DefaultTokenRegex;
            var tokenRegexSetting = variables.GetValue(SpecialVariables.VariableTokenRegex);
            if (!string.IsNullOrWhiteSpace(tokenRegexSetting))
            {
                tokenRegex = new Regex(tokenRegexSetting, RegexOptions.Compiled | RegexOptions.Singleline);
            }

            var config = new Configuration();
            config.IgnoreMissingTokens = variables.GetFlag(SpecialVariables.IgnoreMissingVariableTokens, false);
            config.TokenRegex = tokenRegex;
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
                if (!SpecialVariables.AllowsSubstitution(node.Name))
                    continue;

                var matches = config.TokenRegex.Matches(node.Value);

                foreach (Match match in matches)
                {
                    var name = match.Groups["variable"].Value;
                    var dependency = nodes.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.InvariantCultureIgnoreCase));
                    if (dependency == null)
                    {
                        if (!config.IgnoreMissingTokens)
                        {
                            throw new Exception(string.Format("The variable '{0}' contains a reference to another variable named '{1}', which is not defined. Set the special variable {2} to have Tentacle print the value of all variables to help diagnose this problem.", node.Name, name, SpecialVariables.PrintVariables));
                        }
                    }
                    else
                    {
                        node.AddDependency(dependency);
                    }
                }
            }
        }

        static void PerformSubstitution(IEnumerable<VariableNode> nodes, Configuration config)
        {
            foreach (var node in nodes)
            {
                node.CalculateAndStoreValue(config);
            }
        }

        class Configuration
        {
            public Regex TokenRegex;
            public bool IgnoreMissingTokens;
        }

        class VariableNode
        {
            private readonly Variable variable;
            private readonly HashSet<VariableNode> dependencies = new HashSet<VariableNode>();

            public VariableNode(Variable variable)
            {
                this.variable = variable;
            }

            public string Name { get { return variable.Name; } }

            public string Value { get { return variable.Value ?? string.Empty; } }

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

            public void CalculateAndStoreValue(Configuration config)
            {
                if (dependencies.Count == 0)
                    return;

                foreach (var dependency in dependencies)
                {
                    dependency.CalculateAndStoreValue(config);
                }

                variable.Value = config.TokenRegex.Replace(variable.Value, m =>
                {
                    var dependency = dependencies.FirstOrDefault(x => string.Equals(x.Name, m.Groups["variable"].Value, StringComparison.InvariantCultureIgnoreCase));
                    if (dependency == null) return m.Value;
                    return dependency.Value;
                });

                dependencies.Clear();
            }
        }
    }
}