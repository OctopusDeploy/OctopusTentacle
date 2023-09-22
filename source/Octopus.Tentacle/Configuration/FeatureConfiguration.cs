using System;
using Halibut.Util;
using Octopus.Configuration;

namespace Octopus.Tentacle.Configuration
{
    public class FeatureConfiguration : IFeatureConfiguration
    {
        // These are deliberately public so consumers like Octopus Server and Tentacle can use the configuration keys
        public const string AsyncHalibutSettingName = "Octopus.Features.AsyncHalibut";

        readonly IKeyValueStore settings;

        public FeatureConfiguration(IKeyValueStore settings)
        {
            this.settings = settings;
        }

        // Hard code
        public AsyncHalibutFeature AsyncHalibut => AsyncHalibutFeature.Enabled;
    }

    public interface IFeatureConfiguration
    {
        AsyncHalibutFeature AsyncHalibut { get; }
    }
}