using System;

namespace Octopus.Platform.Variables
{
    public class VariableDefinition
    {
        readonly string name;
        readonly string description;
        readonly string example;
        readonly bool isPattern;
        readonly VariableDomain domain;
        readonly VariableCategory category;

        public VariableDefinition(string name, string description, string example, bool isPattern, VariableDomain domain, VariableCategory category)
        {
            this.name = name;
            this.description = description;
            this.example = example;
            this.isPattern = isPattern;
            this.domain = domain;
            this.category = category;
        }

        public string Name
        {
            get { return name; }
        }

        public string Description
        {
            get { return description; }
        }

        public string Example
        {
            get { return example; }
        }

        public VariableDomain Domain
        {
            get { return domain; }
        }

        public bool IsPattern
        {
            get { return isPattern; }
        }

        public VariableCategory Category
        {
            get { return category; }
        }
    }
}