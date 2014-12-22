using System;
using System.Collections.Generic;

namespace Octopus.Platform.Variables.Templates.Evaluator
{
    public class Binding : Dictionary<string, Binding>
    {
        readonly Dictionary<string, Binding> _indexable; 

        public Binding(string item = null)
            : base(StringComparer.OrdinalIgnoreCase)
        {
            Item = item;
            _indexable = new Dictionary<string, Binding>(StringComparer.OrdinalIgnoreCase);
        }

        public string Item { get; set; }

        public Dictionary<string, Binding> Indexable
        {
            get { return _indexable; }
        }
    }
}