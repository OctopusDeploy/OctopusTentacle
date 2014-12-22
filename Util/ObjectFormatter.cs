using System;
using Newtonsoft.Json;

namespace Octopus.Platform.Util
{
    public static class ObjectFormatter
    {
        public static string Format(object o)
        {
            return JsonConvert.SerializeObject(o);
        }
    }
}
