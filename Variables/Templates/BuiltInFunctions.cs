using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Octopus.Platform.Variables.Templates.Evaluator
{
    public static class BuiltInFunctions
    {
        static readonly IDictionary<string, Func<string, string>> _extensions = new Dictionary<string, Func<string, string>>();
 
        // Configuration shoudl be done at startup, this isn't thread-safe.
        public static void Register(string name, Func<string, string> implementation)
        {
            _extensions.Add(name.ToLowerInvariant(), implementation);
        }

        public static string InvokeOrNull(string function, string[] args)
        {
            if (args.Length != 1)
                return null; // Undefined, will cause source text to print

            var functionName = function.ToLowerInvariant();
            var arg0 = args[0];

            switch (functionName)
            {
                case "tolower":
                    return arg0.ToLower(); // Happy to leave this culture-specific
                case "toupper":
                    return arg0.ToUpper();
                case "htmlescape":
                    return HtmlEscape(arg0);
                case "xmlescape":
                    return XmlEscape(arg0);
                case "jsonescape":
                    return JsonEscape(arg0);
            }

            Func<string, string> ext;
            if (_extensions.TryGetValue(functionName, out ext))
                return ext(arg0);

            return null; // Undefined, will cause source text to print
        }

        static string HtmlEscape(string raw)
        {
            return Escape(raw, HtmlEntityMap);
        }

        static string XmlEscape(string raw)
        {
            return Escape(raw, XmlEntityMap);
        }

        static string JsonEscape(string raw)
        {
            return Escape(raw, JsonEntityMap);
        }

        static string Escape(string raw, IDictionary<char, string> entities)
        {
            return string.Join("", raw.Select(c =>
            {
                string entity;
                if (entities.TryGetValue(c, out entity))
                    return entity;
                return c.ToString(CultureInfo.InvariantCulture);
            }));
        }

        static readonly IDictionary<char, string> HtmlEntityMap = new Dictionary<char, string>
                                                                {
                                                                    { '&', "&amp;" },
                                                                    { '<', "&lt;" },
                                                                    { '>', "&gt;" },
                                                                    { '"', "&quot;" },
                                                                    { '\'', "&apos;" },
                                                                    { '/', "&#x2F;" }
                                                                };

        static readonly IDictionary<char, string> XmlEntityMap = new Dictionary<char, string>
                                                                {
                                                                    { '&', "&amp;" },
                                                                    { '<', "&lt;" },
                                                                    { '>', "&gt;" },
                                                                    { '"', "&quot;" },
                                                                    { '\'', "&apos;" }
                                                                };

        // This is overly simplistic since Unicode chars also need escaping.
        static readonly IDictionary<char, string> JsonEntityMap = new Dictionary<char, string>
                                                                {
                                                                    { '\"', "\\\"" },
                                                                    { '\r', "\\\r" },
                                                                    { '\t', "\\\t" },
                                                                    { '\n', "\\\n" },
                                                                    { '\\', "\\\\" }
                                                                };
    }
}