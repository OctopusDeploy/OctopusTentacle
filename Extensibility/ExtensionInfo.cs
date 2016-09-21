namespace Octopus.Shared.Extensibility
{
    public class ExtensionInfo
    {
        public ExtensionInfo(string name, string assemblyName, bool isCustom = false)
        {
            Name = name;
            AssemblyName = assemblyName;
            IsCustom = isCustom;
        }

        public string Name { get; set; }
        public string AssemblyName { get; set; }

        public bool IsCustom { get; set; }
    }
}