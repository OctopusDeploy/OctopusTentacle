using System;

namespace Octopus.Platform.Variables
{
    public static class VariableDictionaryExtensions
    {
        public static void Evaluate(this VariableDictionary variables)
        {
            VariableEvaluator.Evaluate(variables);
        }
    }
}