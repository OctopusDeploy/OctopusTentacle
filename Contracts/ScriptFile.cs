using System;
using Halibut;

namespace Octopus.Shared.Contracts
{
    public class ScriptFile
    {
        readonly string name;
        readonly DataStream contents;

        public ScriptFile(string name, DataStream contents)
        {
            this.name = name;
            this.contents = contents;
        }

        public string Name
        {
            get { return name; }
        }

        public DataStream Contents
        {
            get { return contents; }
        }
    }
}