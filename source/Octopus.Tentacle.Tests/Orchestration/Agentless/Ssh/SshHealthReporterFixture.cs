//  TODO
// using System;
// using NUnit.Framework;
// using Octopus.Agent.Orchestration.Agentless.Ssh.Health;
//

// namespace Octopus.Tests.Octopus.Tentacle.Orchestration.Agentless.Ssh
// {
//     [TestFixture]
//     public class SshHealthReporterFixture
//     {
//         [Test]
//         public void ParsesDFOutput()
//         {
//             const string output = @"Filesystem     1K-blocks    Used Available Use% Mounted on
// /dev/xvda1       8125880 1168296   6857316  15% /
// devtmpfs          287060      12    287048   1% /dev
// tmpfs             303460       0    303460   0% /dev/shm
// ";

//             var driveInfo = SshHealthReporter.ParseDriveInfo(output, new NullLog());

//             Assert.AreEqual(3, driveInfo.Count);
//             Assert.AreEqual(287048 * 1024, driveInfo["devtmpfs"]);
//         }
//     }
// }

using System;