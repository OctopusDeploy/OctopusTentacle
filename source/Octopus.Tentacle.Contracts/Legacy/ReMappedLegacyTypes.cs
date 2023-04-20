#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Tentacle.Contracts.Legacy
{
    public class ReMappedLegacyTypes
    {
        private static IReadOnlyCollection<Type> Types = new HashSet<Type>(new[] { typeof(CancelScriptCommand),
            typeof(CompleteScriptCommand),
            typeof(IFileTransferService),
            typeof(IScriptService),
            typeof(ScriptFile),
            typeof(ScriptIsolationLevel),
            typeof(ScriptStatusRequest),
            typeof(ScriptStatusResponse),
            typeof(ScriptTicket),
            typeof(ScriptType),
            typeof(StartScriptCommand),
            typeof(TentacleContracts),
            typeof(UploadResult)});

        IReadOnlyCollection<string> FullNameOfTypesToRemap;

        public ReMappedLegacyTypes(params string[] nameSpaces)
        {
            var set = new HashSet<string>();

            foreach (var nameSpace in nameSpaces)
            {
                foreach (var oldTypeName in Types.Select(t => nameSpace + "." + t.Name).ToHashSet())
                {
                    set.Add(oldTypeName);
                }
            }

            FullNameOfTypesToRemap = set;
        }

        public bool ShouldRemap(string? fullTypeName)
        {
            if (fullTypeName == null) return false;

            return FullNameOfTypesToRemap.Contains(fullTypeName);
        }
    }
}