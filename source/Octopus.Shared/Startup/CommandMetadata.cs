#nullable disable
using System;

namespace Octopus.Shared.Startup
{
    public class CommandMetadata
    {
        public string Name { get; set; }
        public string[] Aliases { get; set; }
        public string Description { get; set; }
    }
}