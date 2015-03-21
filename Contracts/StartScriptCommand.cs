using System;
using System.Collections.Generic;
using Halibut;
using Newtonsoft.Json;

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

    public class StartScriptCommand
    {
        readonly string scriptBody;
        readonly ScriptIsolationLevel isolation;
        readonly List<ScriptFile> files = new List<ScriptFile>();

        public StartScriptCommand(string scriptBody, params ScriptFile[] additionalFiles)
            : this(scriptBody, null, additionalFiles)
        {
        }

        [JsonConstructor]
        public StartScriptCommand(string scriptBody, ScriptIsolationLevel isolation)
        {
            this.scriptBody = scriptBody;
            this.isolation = isolation ?? new NoIsolationLevel();
        }

        public StartScriptCommand(string scriptBody, ScriptIsolationLevel isolation, params ScriptFile[] additionalFiles)
            : this(scriptBody, isolation)
        {
            if (additionalFiles != null)
            {
                files.AddRange(additionalFiles);                
            }
        }

        public string ScriptBody
        {
            get { return scriptBody; }
        }

        public ScriptIsolationLevel Isolation
        {
            get { return isolation; }
        }

        public List<ScriptFile> Files
        {
            get { return files; }
        }
    }

    public abstract class ScriptIsolationLevel
    {
    }

    public class NoIsolationLevel : ScriptIsolationLevel
    {
    }

    public class FullIsolationLevel : ScriptIsolationLevel
    {
    }
}