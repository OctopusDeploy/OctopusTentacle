using System;

namespace Octopus.Shared.Variables
{
    public static class VariableDictionaryExtensions
    {
        public static void Evaluate(this VariableDictionary variables)
        {
            VariableEvaluator.Evaluate(variables);
        }
    }
}