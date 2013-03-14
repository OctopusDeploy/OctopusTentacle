using System;

namespace Octopus.Shared.Conventions
{
    public static class ConventionPriority
    {
        public static readonly int PreDeployScript = 0;
        public static readonly int DeletePackageFile = 500;
        public static readonly int ConfigTransforms = 1000;
        public static readonly int ConfigVariables = 1100;
        public static readonly int AzureConfiguration = 1200;
        public static readonly int CopyPackage = 2500;
        public static readonly int DeployScript = 3000;
        public static readonly int IisWebSite = 5000;
        public static readonly int Ftp = 6000;
        public static readonly int AzureUpload = 7000;
        public static readonly int AzureDeployment = 7500;
        public static readonly int PostDeployScript = 10000;
    }
}