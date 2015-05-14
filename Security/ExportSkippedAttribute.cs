using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Octopus.Shared.Security
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ExportSkippedAttribute : Attribute
    {

    }
}
