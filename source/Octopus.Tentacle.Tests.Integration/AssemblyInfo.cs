using NUnit.Framework;
using Octopus.Tentacle.Tests.Integration.Support.TestAttributes;

[assembly: Parallelizable(ParallelScope.All)]
[assembly: CustomLevelOfParallelism]
