using System;
using System.Collections.Generic;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Services
{
    [Service]
    public class SupportedFeaturesService : ISupportedFeaturesService
    {
        public GetFeaturesResponse SupportedFeatures()
        {
            var features = new List<string>();
            // Some example to show what might be in these.
            features.Add("ScriptServiceIsIdempotent");
            features.Add("ScriptServiceDoesNotLieAboutScriptStatus"); // Ironically this is a lie.
            return new GetFeaturesResponse(features.AsReadOnly());
        }
    }
}