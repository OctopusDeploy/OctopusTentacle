using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;

namespace Octopus.Tentacle.Kubernetes.Crypto
{
    public interface IScriptPodLogEncryptionKeyGenerator
    {
        Task<byte[]> GenerateKey(ScriptTicket scriptTicket, int keySizeInBytes, CancellationToken cancellationToken);
    }

    public class ScriptPodLogEncryptionKeyGenerator : IScriptPodLogEncryptionKeyGenerator
    {
        readonly IKubernetesMachineEncryptionKeyProvider machineEncryptionKeyProvider;

        public ScriptPodLogEncryptionKeyGenerator(IKubernetesMachineEncryptionKeyProvider machineEncryptionKeyProvider)
        {
            this.machineEncryptionKeyProvider = machineEncryptionKeyProvider;
        }

        public async Task<byte[]> GenerateKey(ScriptTicket scriptTicket, int keySizeInBytes, CancellationToken cancellationToken)
        {
            var (machineEncryptionKey, _) = await machineEncryptionKeyProvider.GetMachineKey(cancellationToken);

            var pdb = new Pkcs5S2ParametersGenerator(new Sha256Digest());
            
            //we use the machine encryption key as the password and the script ticket is the salt
            pdb.Init(machineEncryptionKey, Encoding.UTF8.GetBytes(scriptTicket.TaskId), 1000);
            var key = (KeyParameter)pdb.GenerateDerivedMacParameters(keySizeInBytes * 8);

            return key.GetKey();
        }
    }
}