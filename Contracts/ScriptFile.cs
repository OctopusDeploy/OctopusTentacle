using Halibut;
using Newtonsoft.Json;

namespace Octopus.Shared.Contracts
{
    public class ScriptFile
    {
        readonly string name;
        readonly DataStream contents;
        readonly string encryptionPassword;

        [JsonConstructor]
        public ScriptFile(string name, DataStream contents, string encryptionPassword)
        {
            this.name = name;
            this.contents = contents;
            this.encryptionPassword = encryptionPassword;
        }

        public ScriptFile(string name, DataStream contents) : this(name, contents, null)
        {
        }

        public string Name
        {
            get { return name; }
        }

        public DataStream Contents
        {
            get { return contents; }
        }

        public string EncryptionPassword
        {
            get { return encryptionPassword; }
        }
    }
}