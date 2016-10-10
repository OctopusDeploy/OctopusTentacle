namespace Octopus.Shared.Extensibility
{
    public class ExtensionInfo
    {
        public ExtensionInfo(string name, string assemblyName, string author, string version, bool isCustom = false)
        {
            Name = name;
            AssemblyName = assemblyName;
            Author = author;
            Version = version;
            IsCustom = isCustom;
        }

        public string Name { get; set; }
        public string AssemblyName { get; set; }
        public string Author { get; set; }

        public string Version { get; set; }

        public bool IsCustom { get; set; }
    }
}