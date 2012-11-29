using System;
using System.Linq;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Octopus.Shared.Activities
{
    [XmlRoot("activity")]
    public class ActivityElement
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("tag")]
        public string Tag { get; set; }
        
        [XmlAttribute("status")]
        [JsonConverter(typeof(StringEnumConverter))]
        public ActivityStatus Status { get; set; }

        [XmlElement("log")]
        public string Log { get; set; }

        [XmlElement("error")]
        public string Error { get; set; }

        [XmlElement("activity")]
        public ActivityElement[] Children { get; set; }

        [XmlAttribute("id")]
        public string Id { get; set; }

        public ActivityElement Clone()
        {
            var clone = new ActivityElement();
            clone.Name = Name;
            clone.Tag = Tag;
            clone.Status = Status;
            clone.Log = Log;
            clone.Error = Error;
            if (Children != null)
            {
                clone.Children = Children.Select(c => c.Clone()).ToArray();
            }

            clone.Id = Id;
            return clone;
        }

        public ActivityStatus? GetStatusForTag(string tag)
        {
            var has = string.Equals(Tag, tag, StringComparison.InvariantCultureIgnoreCase);
            if (has) return Status;

            if (Children == null) return null;
            foreach (var child in Children)
            {
                var status = child.GetStatusForTag(tag);
                if (status != null) return status;
            }

            return null;
        }
    }
}