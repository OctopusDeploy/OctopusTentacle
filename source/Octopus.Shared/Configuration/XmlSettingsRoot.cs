using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Octopus.Shared.Configuration
{
    [XmlRoot("octopus-settings")]
    public class XmlSettingsRoot
    {
        public XmlSettingsRoot()
        {
            Settings = new List<XmlSetting>();
        }

        [XmlElement("set")]
        public List<XmlSetting> Settings { get; set; }
    }
}