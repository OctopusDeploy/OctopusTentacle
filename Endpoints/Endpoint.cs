using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Octopus.Platform.Security.Masking;
using Octopus.Platform.Util;
using Octopus.Platform.Variables;

namespace Octopus.Platform.Model.Endpoints
{
    public abstract class Endpoint
    {
        readonly IDictionary<string, Variable> raw;

        protected Endpoint(IDictionary<string, Variable> raw)
        {
            this.raw = raw;
        }

        protected T GetEndpointProperty<T>([CallerMemberName] string name = null)
        {
            if (name == null) throw new ArgumentNullException("name");
            
            Variable rawValue;
            if (!raw.TryGetValue(name, out rawValue))
                return default(T);

            return (T)AmazingConverter.Convert(rawValue.Value, typeof(T));
        }

        protected void SetEndpointProperty<T>(T value, bool isSensitive = false, [CallerMemberName] string name = null)
        {
            if (name == null) throw new ArgumentNullException("name");
            var svalue = (string)AmazingConverter.Convert(value, typeof(string));
            if (isSensitive)
                MaskingContext.Permanent.MaskInstancesOf(svalue);
            raw[name] = new Variable(name, svalue, isSensitive && svalue != null);
        }

        public override bool Equals(object obj)
        {
            var endpoint = obj as Endpoint;
            return endpoint != null &&
                   endpoint.GetType() == GetType() &&
                   endpoint.Describe().Equals(Describe());
        }

        public override int GetHashCode()
        {
            return Describe().GetHashCode();
        }

        string Describe()
        {
            return string.Join(";", raw
                .OrderBy(v => v.Key)
                .Select(v => v.Key + "=" + v.Value));
        }

        public override string ToString()
        {
            return Describe();
        }

        public IEnumerable<Variable> ToVariables()
        {
            return raw.Values.ToList();
        }
    }
}
