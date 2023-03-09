using System.Collections.Generic;

namespace Octopus.Tentacle.Contracts
{
    public interface ISupportedFeaturesService
    {
        public GetFeaturesResponse SupportedFeatures();
    }
    
    public class GetFeaturesResponse
    {
        public GetFeaturesResponse(IReadOnlyList<string> features)
        {
            Features = features;
        }

        public IReadOnlyList<string> Features { get; }
    }
}