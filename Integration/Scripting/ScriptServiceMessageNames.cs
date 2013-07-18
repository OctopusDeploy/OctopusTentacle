using System;

namespace Octopus.Shared.Integration.Scripting
{
    public class ScriptServiceMessageNames
    {
        public static class SetVariable
        {
            public static string Name = "setVariable";            
            public static string NameAttribute = "name";            
            public static string ValueAttribute = "value";            
        }

        public static class CreateArtifact
        {
            public static string Name = "createArtifact";
            public static string PathAttribute = "path";
        }
    }
}