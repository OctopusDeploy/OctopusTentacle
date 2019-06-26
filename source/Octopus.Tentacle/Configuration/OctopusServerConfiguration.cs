using System;
using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Octopus.Client.Model;
using Octopus.Shared.Util;

namespace Octopus.Tentacle.Configuration
{
    /// <summary>
    /// Describes an Octopus Server that the Tentacle communicates with.
    /// </summary>
    public class OctopusServerConfiguration
    {
        string thumbprint;

        /// <summary>
        /// Create a new OctopusServerConfiguration.
        /// </summary>
        /// <param name="thumbprint"></param>
        public OctopusServerConfiguration(string thumbprint)
        {
            Thumbprint = thumbprint;
        }

        /// <summary>
        /// The server's X509 certificate thumbprint.
        /// </summary>
        public string Thumbprint
        {
            get { return thumbprint; }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("A valid thumbprint must be supplied");

                thumbprint = value.Trim();
            }
        }

        /// <summary>
        /// The communication style used with this server.
        /// </summary>
        [JsonConverter(typeof(CommunicationStyleConverter))]
        public CommunicationStyle CommunicationStyle { get; set; }

        /// <summary>
        /// The URL used when connecting to the server, if available.
        /// </summary>
        public Uri Address { get; set; }

        /// <summary>
        /// The server's unique identifier.
        /// </summary>
        public string Squid { get; set; }

        public string SubscriptionId { get; set; }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        public override string ToString()
        {
            return ObjectFormatter.Format(this);
        }

        class CommunicationStyleConverter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
                =>  writer.WriteValue((int) value);

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.Value == null)
                    return CommunicationStyle.None;

                if (reader.Value is string str)
                    return Enum.Parse(typeof(CommunicationStyle), str);
                
                return (CommunicationStyle) Convert.ToInt32(reader.Value);
            }

            public override bool CanConvert(Type objectType)
                => objectType == typeof(CommunicationStyle);
        }
    }
}