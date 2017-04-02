using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace Octopus.Shared.Internals.Options
{
    public abstract class Option
    {
        static readonly char[] NameTerminator = {'=', ':'};
        readonly string prototype;
        readonly string description;
        readonly string[] names;
        readonly OptionValueType type;
        readonly int count;

        protected Option(string prototype, string description)
            : this(prototype, description, 1)
        {
        }

        protected Option(string prototype, string description, int maxValueCount)
        {
            if (prototype == null)
                throw new ArgumentNullException("prototype");
            if (prototype.Length == 0)
                throw new ArgumentException("Cannot be the empty string.", "prototype");
            if (maxValueCount < 0)
                throw new ArgumentOutOfRangeException("maxValueCount");

            this.prototype = prototype;
            names = prototype.Split('|');
            this.description = description;
            count = maxValueCount;
            type = ParsePrototype();

            if (count == 0 && type != OptionValueType.None)
                throw new ArgumentException(
                    "Cannot provide maxValueCount of 0 for OptionValueType.Required or " +
                        "OptionValueType.Optional.",
                    "maxValueCount");
            if (type == OptionValueType.None && maxValueCount > 1)
                throw new ArgumentException(
                    string.Format("Cannot provide maxValueCount of {0} for OptionValueType.None.", maxValueCount),
                    "maxValueCount");
            if (Array.IndexOf(names, "<>") >= 0 &&
                ((names.Length == 1 && type != OptionValueType.None) ||
                    (names.Length > 1 && MaxValueCount > 1)))
                throw new ArgumentException(
                    "The default option handler '<>' cannot require values.",
                    "prototype");
        }

        public string Prototype
        {
            get { return prototype; }
        }

        public string Description
        {
            get { return description; }
        }

        public OptionValueType OptionValueType
        {
            get { return type; }
        }

        public int MaxValueCount
        {
            get { return count; }
        }

        internal string[] Names
        {
            get { return names; }
        }

        internal string[] ValueSeparators { get; set; }
        public bool Hide { get; set; }

        public string[] GetNames()
        {
            return (string[])names.Clone();
        }

        public string[] GetValueSeparators()
        {
            if (ValueSeparators == null)
                return new string[0];
            return (string[])ValueSeparators.Clone();
        }

        protected static T Parse<T>(string value, OptionContext c)
        {
            var conv = TypeDescriptor.GetConverter(typeof (T));
            var t = default(T);
            try
            {
                if (value != null)
                    t = (T)conv.ConvertFromString(value);
            }
            catch (Exception e)
            {
                throw new OptionException(
                    string.Format(
                        c.OptionSet.MessageLocalizer("Could not convert string `{0}' to type {1} for option `{2}'."),
                        value, typeof (T).Name, c.OptionName),
                    c.OptionName, e);
            }
            return t;
        }

        OptionValueType ParsePrototype()
        {
            var type = '\0';
            var seps = new List<string>();
            for (var i = 0; i < names.Length; ++i)
            {
                var name = names[i];
                if (name.Length == 0)
                    throw new ArgumentException("Empty option names are not supported.", "prototype");

                var end = name.IndexOfAny(NameTerminator);
                if (end == -1)
                    continue;
                names[i] = name.Substring(0, end);
                if (type == '\0' || type == name[end])
                    type = name[end];
                else
                    throw new ArgumentException(
                        string.Format("Conflicting option types: '{0}' vs. '{1}'.", type, name[end]),
                        "prototype");
                AddSeparators(name, end, seps);
            }

            if (type == '\0')
                return OptionValueType.None;

            if (count <= 1 && seps.Count != 0)
                throw new ArgumentException(
                    string.Format("Cannot provide key/value separators for Options taking {0} value(s).", count),
                    "prototype");
            if (count > 1)
            {
                if (seps.Count == 0)
                    ValueSeparators = new[] {":", "="};
                else if (seps.Count == 1 && seps[0].Length == 0)
                    ValueSeparators = null;
                else
                    ValueSeparators = seps.ToArray();
            }

            return type == '=' ? OptionValueType.Required : OptionValueType.Optional;
        }

        static void AddSeparators(string name, int end, ICollection<string> seps)
        {
            var start = -1;
            for (var i = end + 1; i < name.Length; ++i)
            {
                switch (name[i])
                {
                    case '{':
                        if (start != -1)
                            throw new ArgumentException(
                                string.Format("Ill-formed name/value separator found in \"{0}\".", name),
                                "prototype");
                        start = i + 1;
                        break;
                    case '}':
                        if (start == -1)
                            throw new ArgumentException(
                                string.Format("Ill-formed name/value separator found in \"{0}\".", name),
                                "prototype");
                        seps.Add(name.Substring(start, i - start));
                        start = -1;
                        break;
                    default:
                        if (start == -1)
                            seps.Add(name[i].ToString(CultureInfo.InvariantCulture));
                        break;
                }
            }
            if (start != -1)
                throw new ArgumentException(
                    string.Format("Ill-formed name/value separator found in \"{0}\".", name),
                    "prototype");
        }

        public void Invoke(OptionContext c)
        {
            OnParseComplete(c);
            c.OptionName = null;
            c.Option = null;
            c.OptionValues.Clear();
        }

        protected abstract void OnParseComplete(OptionContext c);

        public override string ToString()
        {
            return Prototype;
        }
    }
}