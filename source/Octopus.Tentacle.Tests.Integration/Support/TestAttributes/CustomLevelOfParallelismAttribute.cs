using System;
using NUnit.Framework;
using NUnit.Framework.Api;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Support.TestAttributes
{
    /// <summary>
    /// Halibut tests take time but don't use lots of CPU.
    ///
    /// For local development by default increase the level of parallelism to 2x the number of Cores.
    ///
    /// This can be overriden with the environment variable "CustomLevelOfParallelism" e.g.
    /// CustomLevelOfParallelism=256
    ///
    /// When run in the build the default level is always used.
    /// </summary>
    [AttributeUsage( AttributeTargets.Assembly, AllowMultiple=false, Inherited=false )]
    public class CustomLevelOfParallelismAttribute : PropertyAttribute
    {
        public CustomLevelOfParallelismAttribute() : base(LevelOfParallelismAttributePropertyName(), LevelOfParallelism())
        {
        }
        
        public static int LevelOfParallelism()
        {
            if (TeamCityDetection.IsRunningInTeamCity())
            {
                return LevelOfParallelismFromEnvVar() ?? NUnitTestAssemblyRunner.DefaultLevelOfParallelism * 2;
            }
            
            return LevelOfParallelismFromEnvVar() ?? NUnitTestAssemblyRunner.DefaultLevelOfParallelism * 2;
        }

        static int? LevelOfParallelismFromEnvVar()
        {
            var nunitLevelOfParallelismSetting = Environment.GetEnvironmentVariable("CustomLevelOfParallelism");
            if (!string.IsNullOrEmpty(nunitLevelOfParallelismSetting))
            {
                if (int.TryParse(nunitLevelOfParallelismSetting, out var level))
                {
                    return level;
                }
            }

            return null;
        }

        static string LevelOfParallelismAttributePropertyName()
        {
            string propertyName = typeof(LevelOfParallelismAttribute).Name;
            if ( propertyName.EndsWith( "Attribute" ) )
                propertyName = propertyName.Substring( 0, propertyName.Length - 9 );
            return propertyName;
        }
    }
}