using System;
using System.Xml.Serialization;

namespace Octopus.Shared.Activities
{
    [XmlRoot("activity")]
    public class ActivityElement
    {
        [XmlAttribute("name")]
        public string Name { get; set; }
        
        [XmlAttribute("status")]
        public ActivityStatus Status { get; set; }

        [XmlElement("log")]
        public string Log { get; set; }

        [XmlElement("error")]
        public string Error { get; set; }

        [XmlElement("activity")]
        public ActivityElement[] Children { get; set; }

        [XmlIgnore]
        public int Id { get; set; }
    }
}