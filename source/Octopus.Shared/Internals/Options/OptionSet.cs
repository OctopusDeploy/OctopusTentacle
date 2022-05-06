using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Octopus.Shared.Internals.Options
{
    public class OptionSet : KeyedCollection<string, Option>
    {
        const int OptionWidth = 29;

        readonly Regex ValueOption = new Regex(
            @"^(?<flag>--|-|/)(?<name>[^:=]+)((?<sep>[:=])(?<value>.*))?$");

        Action<string[]>? leftovers;

        public OptionSet()
            : this(delegate(string f)
            {
                return f;
            })
        {
        }

        public OptionSet(Converter<string, string> localizer)
            : base(StringComparer.OrdinalIgnoreCase)
        {
            MessageLocalizer = localizer;
        }

        public Converter<string, string> MessageLocalizer { get; }

        protected override string GetKeyForItem(Option item)
        {
            if (item == null)
                throw new ArgumentNullException("option");
            if (item.Names != null && item.Names.Length > 0)
                return item.Names[0];
            // This should never happen, as it's invalid for Option to be
            // constructed w/o any names.
            throw new InvalidOperationException("Option has no names!");
        }

        public Option? GetOptionForName(string option)
        {
            if (option == null)
                throw new ArgumentNullException("option");

            if (!Contains(option))
                return null;

            return base[option];
        }

        protected override void InsertItem(int index, Option item)
        {
            base.InsertItem(index, item);
            AddImpl(item);
        }

        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);
            var p = Items[index];
            // KeyedCollection.RemoveItem() handles the 0th item
            for (var i = 1; i < p.Names.Length; ++i)
                Dictionary.Remove(p.Names[i]);
        }

        protected override void SetItem(int index, Option item)
        {
            base.SetItem(index, item);
            RemoveItem(index);
            AddImpl(item);
        }

        void AddImpl(Option option)
        {
            if (option == null)
                throw new ArgumentNullException("option");
            var added = new List<string>(option.Names.Length);
            try
            {
                // KeyedCollection.InsertItem/SetItem handle the 0th name.
                for (var i = 1; i < option.Names.Length; ++i)
                {
                    Dictionary.Add(option.Names[i], option);
                    added.Add(option.Names[i]);
                }
            }
            catch (Exception)
            {
                foreach (var name in added)
                    Dictionary.Remove(name);
                throw;
            }
        }

        public new OptionSet Add(Option option)
        {
            base.Add(option);
            return this;
        }

        public OptionSet Add(string prototype, Action<string?> action)
            => Add(prototype, null, action);

        public OptionSet Add(string prototype,
            string? description,
            Action<string> action,
            bool hide = false,
            bool sensitive = false)
        {
            if (action == null)
                throw new ArgumentNullException("action");
            Option p = new ActionOption(prototype,
                description,
                1,
                delegate(OptionValueCollection v)
                {
                    action(v[0] ?? string.Empty);
                });
            p.Hide = hide;
            p.Sensitive = sensitive;
            base.Add(p);
            return this;
        }

        public OptionSet Add(string prototype, OptionAction<string?, string?> action)
            => Add(prototype, null, action);

        public OptionSet Add(string prototype, string? description, OptionAction<string?, string?> action)
        {
            if (action == null)
                throw new ArgumentNullException("action");
            Option p = new ActionOption(prototype,
                description,
                2,
                delegate(OptionValueCollection v)
                {
                    action(v[0], v[1]);
                });
            base.Add(p);
            return this;
        }

        public OptionSet Add<T>(string prototype, Action<T> action)
            => Add(prototype, null, action);

        public OptionSet Add<T>(string prototype, string? description, Action<T> action)
            => Add(new ActionOption<T>(prototype, description, action));

        public OptionSet Add<TKey, TValue>(string prototype, OptionAction<TKey, TValue> action)
            => Add(prototype, null, action);

        public OptionSet Add<TKey, TValue>(string prototype, string? description, OptionAction<TKey, TValue> action)
            => Add(new ActionOption<TKey, TValue>(prototype, description, action));

        protected virtual OptionContext CreateOptionContext()
            => new OptionContext(this);

        public OptionSet WithExtras(Action<string[]> leftovers)
        {
            this.leftovers = leftovers;
            return this;
        }

        public List<string> Parse(IEnumerable<string> arguments)
        {
            var process = true;
            var c = CreateOptionContext();
            c.OptionIndex = -1;
            var def = GetOptionForName("<>");
            var unprocessed =
                from argument in arguments
                where ++c.OptionIndex >= 0 && (process || def != null)
                    ? process
                        ? argument == "--"
                            ? process = false
                            : !Parse(argument, c)
                                ? def != null
                                    ? Unprocessed(def, c, argument)
                                    : true
                                : false
                        : def != null
                            ? Unprocessed(def, c, argument)
                            : true
                    : true
                select argument;
            var r = unprocessed.ToList();

            c.Option?.Invoke(c);

            if (leftovers != null && r.Count > 0)
                leftovers(r.ToArray());

            return r;
        }

        static bool Unprocessed(Option def, OptionContext c, string argument)
        {
            if (def == null)
                return false;
            c.OptionValues.Add(argument);
            c.Option = def;
            c.Option.Invoke(c);
            return false;
        }

        protected bool GetOptionParts(string argument,
            out OptionParts? parts)
        {
            if (argument == null)
                throw new ArgumentNullException("argument");

            parts = null;
            var m = ValueOption.Match(argument);
            if (!m.Success)
                return false;
            parts = new OptionParts(m.Groups["flag"].Value, m.Groups["name"].Value);
            if (m.Groups["sep"].Success && m.Groups["value"].Success)
            {
                parts.Separator = m.Groups["sep"].Value;
                parts.Value = m.Groups["value"].Value;
            }

            return true;
        }

        protected virtual bool Parse(string argument, OptionContext c)
        {
            if (c.Option != null)
            {
                ParseValue(argument, c);
                return true;
            }

            if (!GetOptionParts(argument, out var parts))
                return false;
            if (parts == null) // this can't happen, but will keep the compiler happy
                return false;

            Option p;
            if (Contains(parts.Name))
            {
                p = this[parts.Name];
                c.OptionName = parts.Flag + parts.Name;
                c.Option = p;
                switch (p.OptionValueType)
                {
                    case OptionValueType.None:
                        c.OptionValues.Add(parts.Name);
                        c.Option.Invoke(c);
                        break;
                    case OptionValueType.Optional:
                    case OptionValueType.Required:
                        ParseValue(parts.Value, c);
                        break;
                }

                return true;
            }

            // no match; is it a bool option?
            if (ParseBool(argument, parts.Name, c))
                return true;
            // is it a bundled option?
            if (ParseBundledValue(parts.Flag, string.Concat(parts.Name + parts.Separator + parts.Value), c))
                return true;

            return false;
        }

        void ParseValue(string? option, OptionContext c)
        {
            if (option != null)
                foreach (var o in c.Option?.ValueSeparators != null
                    ? option.Split(c.Option.ValueSeparators, StringSplitOptions.None)
                    : new[] { option })
                    c.OptionValues.Add(o);
            if (c.OptionValues.Count == c.Option?.MaxValueCount ||
                c.Option?.OptionValueType == OptionValueType.Optional)
                c.Option.Invoke(c);
            else if (c.OptionValues.Count > c.Option?.MaxValueCount)
                throw new OptionException(MessageLocalizer(string.Format(
                        "Error: Found {0} option values when expecting {1}.",
                        c.OptionValues.Count,
                        c.Option.MaxValueCount)),
                    c.OptionName ?? string.Empty);
        }

        bool ParseBool(string option, string n, OptionContext c)
        {
            string rn;
            if (n.Length >= 1 && (n[n.Length - 1] == '+' || n[n.Length - 1] == '-') &&
                Contains(rn = n.Substring(0, n.Length - 1)))
            {
                var p = this[rn];
                var v = n[n.Length - 1] == '+' ? option : null;
                c.OptionName = option;
                c.Option = p;
                c.OptionValues.Add(v);
                p.Invoke(c);
                return true;
            }

            return false;
        }

        bool ParseBundledValue(string f, string n, OptionContext c)
        {
            if (f != "-")
                return false;
            for (var i = 0; i < n.Length; ++i)
            {
                Option p;
                var opt = f + n[i];
                var rn = n[i].ToString();
                if (!Contains(rn))
                {
                    if (i == 0)
                        return false;
                    throw new OptionException(string.Format(MessageLocalizer(
                                "Cannot bundle unregistered option '{0}'."),
                            opt),
                        opt);
                }

                p = this[rn];
                switch (p.OptionValueType)
                {
                    case OptionValueType.None:
                        Invoke(c, opt, n, p);
                        break;
                    case OptionValueType.Optional:
                    case OptionValueType.Required:
                    {
                        var v = n.Substring(i + 1);
                        c.Option = p;
                        c.OptionName = opt;
                        ParseValue(v.Length != 0 ? v : null, c);
                        return true;
                    }
                    default:
                        throw new InvalidOperationException("Unknown OptionValueType: " + p.OptionValueType);
                }
            }

            return true;
        }

        static void Invoke(OptionContext c, string name, string value, Option option)
        {
            c.OptionName = name;
            c.Option = option;
            c.OptionValues.Add(value);
            option.Invoke(c);
        }

        public void WriteOptionDescriptions(TextWriter o)
        {
            foreach (var p in this.Where(p => !p.Hide))
            {
                var written = 0;
                if (!WriteOptionPrototype(o, p, ref written))
                    continue;

                if (written < OptionWidth)
                {
                    o.Write(new string(' ', OptionWidth - written));
                }
                else
                {
                    o.WriteLine();
                    o.Write(new string(' ', OptionWidth));
                }

                var lines = GetLines(MessageLocalizer(GetDescription(p.Description)));
                o.WriteLine(lines[0]);
                var prefix = new string(' ', OptionWidth + 2);
                for (var i = 1; i < lines.Count; ++i)
                {
                    o.Write(prefix);
                    o.WriteLine(lines[i]);
                }
            }
        }

        bool WriteOptionPrototype(TextWriter o, Option p, ref int written)
        {
            var names = p.Names;

            var i = GetNextOptionIndex(names, 0);
            if (i == names.Length)
                return false;

            if (names[i].Length == 1)
            {
                Write(o, ref written, "  -");
                Write(o, ref written, names[0]);
            }
            else
            {
                Write(o, ref written, "      --");
                Write(o, ref written, names[0]);
            }

            for (i = GetNextOptionIndex(names, i + 1);
                i < names.Length;
                i = GetNextOptionIndex(names, i + 1))
            {
                Write(o, ref written, ", ");
                Write(o, ref written, names[i].Length == 1 ? "-" : "--");
                Write(o, ref written, names[i]);
            }

            if (p.OptionValueType == OptionValueType.Optional ||
                p.OptionValueType == OptionValueType.Required)
            {
                if (p.OptionValueType == OptionValueType.Optional)
                    Write(o, ref written, MessageLocalizer("["));
                Write(o, ref written, MessageLocalizer("=" + GetArgumentName(0, p.MaxValueCount, p.Description)));
                var sep = p.ValueSeparators != null && p.ValueSeparators.Length > 0
                    ? p.ValueSeparators[0]
                    : " ";
                for (var c = 1; c < p.MaxValueCount; ++c)
                    Write(o, ref written, MessageLocalizer(sep + GetArgumentName(c, p.MaxValueCount, p.Description)));
                if (p.OptionValueType == OptionValueType.Optional)
                    Write(o, ref written, MessageLocalizer("]"));
            }

            return true;
        }

        static int GetNextOptionIndex(string[] names, int i)
        {
            while (i < names.Length && names[i] == "<>")
                ++i;
            return i;
        }

        static void Write(TextWriter o, ref int n, string s)
        {
            n += s.Length;
            o.Write(s);
        }

        static string GetArgumentName(int index, int maxIndex, string? description)
        {
            if (description == null)
                return maxIndex == 1 ? "VALUE" : "VALUE" + (index + 1);
            string[] nameStart;
            if (maxIndex == 1)
                nameStart = new[] { "{0:", "{" };
            else
                nameStart = new[] { "{" + index + ":" };
            for (var i = 0; i < nameStart.Length; ++i)
            {
                int start, j = 0;
                do
                {
                    start = description.IndexOf(nameStart[i], j);
                } while (start >= 0 && j != 0 ? description[j++ - 1] == '{' : false);

                if (start == -1)
                    continue;
                var end = description.IndexOf("}", start);
                if (end == -1)
                    continue;
                return description.Substring(start + nameStart[i].Length, end - start - nameStart[i].Length);
            }

            return maxIndex == 1 ? "VALUE" : "VALUE" + (index + 1);
        }

        static string GetDescription(string? description)
        {
            if (description == null)
                return string.Empty;
            var sb = new StringBuilder(description.Length);
            var start = -1;
            for (var i = 0; i < description.Length; ++i)
                switch (description[i])
                {
                    case '{':
                        if (i == start)
                        {
                            sb.Append('{');
                            start = -1;
                        }
                        else if (start < 0)
                        {
                            start = i + 1;
                        }

                        break;
                    case '}':
                        if (start < 0)
                        {
                            if (i + 1 == description.Length || description[i + 1] != '}')
                                throw new InvalidOperationException("Invalid option description: " + description);
                            ++i;
                            sb.Append("}");
                        }
                        else
                        {
                            sb.Append(description.Substring(start, i - start));
                            start = -1;
                        }

                        break;
                    case ':':
                        if (start < 0)
                            goto default;
                        start = i + 1;
                        break;
                    default:
                        if (start < 0)
                            sb.Append(description[i]);
                        break;
                }

            return sb.ToString();
        }

        static List<string> GetLines(string description)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(description))
            {
                lines.Add(string.Empty);
                return lines;
            }

            var length = 80 - OptionWidth - 2;
            int start = 0, end;
            do
            {
                end = GetLineEnd(start, length, description);
                var cont = false;
                if (end < description.Length)
                {
                    var c = description[end];
                    if (c == '-' || char.IsWhiteSpace(c) && c != '\n')
                    {
                        ++end;
                    }
                    else if (c != '\n')
                    {
                        cont = true;
                        --end;
                    }
                }

                lines.Add(description.Substring(start, end - start));
                if (cont)
                    lines[lines.Count - 1] += "-";
                start = end;
                if (start < description.Length && description[start] == '\n')
                    ++start;
            } while (end < description.Length);

            return lines;
        }

        static int GetLineEnd(int start, int length, string description)
        {
            var end = Math.Min(start + length, description.Length);
            var sep = -1;
            for (var i = start; i < end; ++i)
                switch (description[i])
                {
                    case ' ':
                    case '\t':
                    case '\v':
                    case '-':
                    case ',':
                    case '.':
                    case ';':
                        sep = i;
                        break;
                    case '\n':
                        return i;
                }

            if (sep == -1 || end == description.Length)
                return end;
            return sep;
        }

        protected class OptionParts
        {
            public OptionParts(string flag, string name)
            {
                Flag = flag;
                Name = name;
            }

            public string Flag { get; }
            public string Name { get; }
            public string? Separator { get; set; }
            public string? Value { get; set; }
        }

        sealed class ActionOption : Option
        {
            readonly Action<OptionValueCollection> action;

            public ActionOption(string prototype, string? description, int count, Action<OptionValueCollection> action)
                : base(prototype, description, count)
            {
                this.action = action;
            }

            protected override void OnParseComplete(OptionContext c)
            {
                action(c.OptionValues);
            }
        }

        sealed class ActionOption<T> : Option
        {
            readonly Action<T> action;

            public ActionOption(string prototype, string? description, Action<T> action)
                : base(prototype, description, 1)
            {
                if (action == null)
                    throw new ArgumentNullException("action");
                this.action = action;
            }

            protected override void OnParseComplete(OptionContext c)
            {
                action(Parse<T>(c.OptionValues[0], c));
            }
        }

        sealed class ActionOption<TKey, TValue> : Option
        {
            readonly OptionAction<TKey, TValue> action;

            public ActionOption(string prototype, string? description, OptionAction<TKey, TValue> action)
                : base(prototype, description, 2)
            {
                if (action == null)
                    throw new ArgumentNullException("action");
                this.action = action;
            }

            protected override void OnParseComplete(OptionContext c)
            {
                action(
                    Parse<TKey>(c.OptionValues[0], c),
                    Parse<TValue>(c.OptionValues[1], c));
            }
        }
    }
}