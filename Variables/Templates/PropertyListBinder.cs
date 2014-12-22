using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Shared.Variables.Templates
{
    public static class PropertyListBinder
    {
        public static Binding CreateFrom(IDictionary<string, string> properties)
        {
            var result = new Binding();
            foreach (var property in properties)
            {
                SymbolExpression pathExpression;
                if (TemplateParser.TryParseIdentifierPath(property.Key, out pathExpression))
                {
                    Add(result, pathExpression.Steps, property.Value ?? "");
                }
            }
            return result;
        }

        static void Add(Binding result, IEnumerable<SymbolExpressionStep> steps, string value)
        {
            var first = steps.FirstOrDefault();

            if (first == null)
            {
                result.Item = value;
                return;
            }

            Binding next;

            var iss = first as Identifier;
            if (iss != null)
            {
                if (!result.TryGetValue(iss.Text, out next))
                {
                    result[iss.Text] = next = new Binding();
                }
            }
            else
            {
                var ix = first as Indexer;
                if (ix != null)
                {
                    if (!result.Indexable.TryGetValue(ix.Index, out next))
                    {
                        result.Indexable[ix.Index] = next = new Binding(ix.Index);
                    }
                }
                else
                {
                    throw new NotImplementedException("Unknown step type: " + first);
                }
            }

            Add(next, steps.Skip(1), value);
        }
    }
}
