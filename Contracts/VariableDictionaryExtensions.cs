using System;

namespace Octopus.Shared.Contracts
{
    public static class VariableDictionaryExtensions
    {
        public static void Evaluate(this VariableDictionary variables)
        {
            var evaluator = new VariableEvaluator();
            evaluator.Evaluate(variables);
        }
    }
}