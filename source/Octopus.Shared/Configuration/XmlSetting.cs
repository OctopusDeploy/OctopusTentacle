using System;
using System.Xml.Serialization;

namespace Octopus.Shared.Configuration
{
    public class XmlSetting
    {
        [XmlAttribute("key")]
        public string Key { get; set; }

        [XmlText]
        public string Value { get; set; }
    }
}