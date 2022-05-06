using System;
using Newtonsoft.Json;

namespace Octopus.Shared.Util
{
    public static class ObjectFormatter
    {
        public static string Format(object o)
            => JsonConvert.SerializeObject(o);
    }
}