using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Octopus.Shared.Variables.Templates
{
    public static class TemplateEvaluator
    {
        public static void Evaluate(Template template, Binding properties, TextWriter output, bool echoMissingTokens = false)
        {
            var context = new EvaluationContext(properties, output);
            Evaluate(template.Tokens, context, echoMissingTokens);  
        }

        static void Evaluate(IEnumerable<TemplateToken> tokens, EvaluationContext context, bool echoMissingTokens) 
        {
            foreach (var token in tokens)
            {
                Evaluate(token, context, echoMissingTokens);
            }
        }

        static void Evaluate(TemplateToken token, EvaluationContext context, bool echoMissingTokens)
        {
            var tt = token as TextToken;
            if (tt != null)
            {
                context.Output.Write(tt.Text);
                return;
            }

            var st = token as SubstitutionToken;
            if (st != null)
            {
                var value = Calculate(st.Expression, context);
                context.Output.Write(value ?? (echoMissingTokens ? st.ToString() : ""));
                return;
            }

            var ct = token as ConditionalToken;
            if (ct != null)
            {
                var value = context.Resolve(ct.Expression);
                if (IsTruthy(value))
                    Evaluate(ct.TruthyTemplate, context, echoMissingTokens);
                else
                    Evaluate(ct.FalsyTemplate, context, echoMissingTokens);
                return;
            }

            var rt = token as RepetitionToken;
            if (rt != null)
            {
                var items = context.ResolveAll(rt.Collection).ToArray();

                for (var i = 0; i < items.Length; ++i)
                {
                    var item = items[i];

                    var specials = new Dictionary<string, string>();

                    if (i == 0)
                        specials.Add(Constants.Each.First, "True");

                    if (i == items.Length - 1)
                        specials.Add(Constants.Each.Last, "True");

                    var locals = PropertyListBinder.CreateFrom(specials);

                    locals.Add(rt.Enumerator.Text, item);

                    var newContext = context.BeginChild(locals);
                    Evaluate(rt.Template, newContext, echoMissingTokens);
                }
                return;
            }

            throw new NotImplementedException("Unknown token type: " + token);
        }

        static string Calculate(ContentExpression expression, EvaluationContext context)
        {
            var sx = expression as SymbolExpression;
            if (sx != null)
                return context.ResolveOptional(sx);

            var fx = expression as FunctionCallExpression;
            if (fx != null)
            {
                var args = fx.Arguments.Select(a => Calculate(a, context)).ToArray();
                if (args.Any(a => a == null))
                    return null; // If any argument is undefined, we fail the whole shebang

                return BuiltInFunctions.InvokeOrNull(fx.Function, args);
            }

            throw new NotImplementedException("Unknown expression type: " + expression);
        }

        static bool IsTruthy(string value)
        {
            return value != "0" &&
                value != "" &&
                string.Compare(value, "no", StringComparison.OrdinalIgnoreCase) != 0 &&
                string.Compare(value, "false", StringComparison.OrdinalIgnoreCase) != 0;
        }
    }
}
