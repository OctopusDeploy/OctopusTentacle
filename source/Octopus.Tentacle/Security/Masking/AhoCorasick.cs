#nullable disable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Octopus.Tentacle.Security.Masking
{
    public class AhoCorasick
    {
        private readonly Node root = new();

        public void Add(string value)
        {
            var node = root;

            foreach (var c in value)
            {
                var child = node[c];

                if (child == null)
                    child = node[c] = new Node(c, node);

                node = child;
            }

            node.Value = value;
        }

        public void Build()
        {
            var queue = new Queue<Node>();

            root.Fail = root;
            foreach (var child in root)
                queue.Enqueue(child);

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();

                foreach (var child in node)
                    queue.Enqueue(child);

                var fail = node.Parent.Fail;

                while (fail[node.Prefix] == null && fail != root)
                    fail = fail.Fail;

                node.Fail = fail[node.Prefix] ?? root;
                if (node.Fail == node)
                    node.Fail = root;
            }
        }

        public FindResult Find(string text, string path = "")
        {
            var index = 0;
            var result = new FindResult();

            var node = root;

            foreach (var c in path)
                node = node[c];

            foreach (var c in text)
            {
                while (node[c] == null && node != root)
                {
                    result.IsPartial = false;
                    node = node.Fail;
                }

                node = node[c] ?? root;

                for (var t = node; t != root; t = t.Fail)
                {
                    result.IsPartial = node.ChildCount > 0;

                    if (!string.IsNullOrEmpty(t.Value))
                        result.Found.Add(new KeyValuePair<int, string>(index, t.Path));
                }

                index++;
            }

            if (result.IsPartial)
                result.PartialPath = node.Path;

            return result;
        }

        [DebuggerDisplay("{Prefix} | Children: {ChildCount}")]
        [DebuggerTypeProxy(typeof(NodeDebugView))]
        private class Node : IEnumerable<Node>
        {
            private Dictionary<char, Node> children;
            private char singleKey;
            private Node singleNode;

            public Node()
            {
            }

            public Node(char prefix, Node parent)
            {
                Prefix = prefix;
                Parent = parent;
            }

            public char Prefix { get; }

            public string Path
            {
                get
                {
                    ICollection<char> chars = new List<char>();
                    var node = this;
                    while (node.Parent != null)
                    {
                        chars.Add(node.Prefix);
                        node = node.Parent;
                    }

                    return new string(chars.Reverse().ToArray());
                }
            }

            public Node Parent { get; }

            public Node Fail { get; set; }

            public string Value { get; set; }

            public int ChildCount => children != null ? children.Count : singleNode != null ? 1 : 0;

            public Node this[char c]
            {
                get
                {
                    if (children != null && children.TryGetValue(c, out var node))
                        return node;
                    if (singleNode != null && c == singleKey)
                        return singleNode;
                    return null;
                }
                set
                {
                    if (singleNode == null)
                    {
                        singleKey = c;
                        singleNode = value;
                        return;
                    }

                    if (children == null)
                        children = new Dictionary<char, Node> { { singleKey, singleNode } };
                    children[c] = value;
                }
            }

            public IEnumerator<Node> GetEnumerator()
            {
                if (children != null)
                    return children.Values.GetEnumerator();
                if (singleNode != null)
                    return ((IEnumerable<Node>)new[] { singleNode }).GetEnumerator();
                return Enumerable.Empty<Node>().GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public override string ToString()
            {
                return Prefix.ToString();
            }

            private sealed class NodeDebugView
            {
                private readonly Node node;

                public NodeDebugView(Node node)
                {
                    this.node = node;
                }
            }
        }

        [DebuggerDisplay("Partial {IsPartial} | PartialPath {PartialPath} | Count {Found.Count}")]
        public class FindResult
        {
            public FindResult()
            {
                Found = new List<KeyValuePair<int, string>>();
            }

            public bool IsPartial { get; set; }

            public string PartialPath { get; set; }

            public ICollection<KeyValuePair<int, string>> Found { get; }
        }
    }
}