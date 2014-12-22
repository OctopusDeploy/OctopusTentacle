using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Octopus.Shared.Variables.Templates
{
    class EvaluationContext
    {
        readonly Binding _binding;
        readonly TextWriter _output;
        readonly EvaluationContext _parent;

        public EvaluationContext(Binding binding, TextWriter output, EvaluationContext parent = null)
        {
            _binding = binding;
            _output = output;
            _parent = parent;
        }

        public TextWriter Output
        {
            get { return _output; }
        }

        public string Resolve(SymbolExpression expression)
        {
            var val = WalkTo(expression);
            if (val == null) return "";
            return val.Item ?? "";
        }

        public string ResolveOptional(SymbolExpression expression)
        {
            var val = WalkTo(expression);
            if (val == null) return null;
            return val.Item;
        }

        Binding WalkTo(SymbolExpression expression)
        {
            var val = _binding;

            foreach (var step in expression.Steps)
            {
                var iss = step as Identifier;
                if (iss != null)
                {
                    if (val.TryGetValue(iss.Text, out val))
                        continue;
                }
                else
                {
                    var ix = step as Indexer;
                    if (ix != null)
                    {
                        if (val.Indexable.TryGetValue(ix.Index, out val))
                            continue;
                    }
                    else
                    {
                        throw new NotImplementedException("Unknown step type: " + step);
                    }
                }

                if (_parent == null)
                    return null;

                return _parent.WalkTo(expression);
            }

            return val;
        }

        public IEnumerable<Binding> ResolveAll(SymbolExpression collection)
        {
            var val = WalkTo(collection);
            if (val == null) return Enumerable.Empty<Binding>();

            if (val.Indexable.Count != 0)
                return val.Indexable.Select(c => c.Value);

            if (val.Item != null)
                return val.Item.Split(',').Select(s => new Binding(s));

            return Enumerable.Empty<Binding>();
        }

        static bool IsNumeric(string arg)
        {
            int unused;
            return int.TryParse(arg, out unused);
        }

        public EvaluationContext BeginChild(Binding locals)
        {
            return new EvaluationContext(locals, Output, this);
        }
    }
}