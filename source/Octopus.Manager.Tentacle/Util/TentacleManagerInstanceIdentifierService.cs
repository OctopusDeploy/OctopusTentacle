using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Octopus.Manager.Tentacle.Util
{
    public interface ITentacleManagerInstanceIdentifierService
    {
        Task<string> GetIdentifier();
    }

    public class TentacleManagerInstanceIdentifierService : ITentacleManagerInstanceIdentifierService
    {
        readonly string identifierFilePath;

        public TentacleManagerInstanceIdentifierService(string identifierFilePath)
        {
            this.identifierFilePath = identifierFilePath;
        }

        public async Task<string> GetIdentifier()
        {
            if (File.Exists(identifierFilePath))
            {
                var fileContentsLines = File.ReadAllLines(identifierFilePath);
                if (Guid.TryParseExact(fileContentsLines.First(), "N", out var id))
                {
                    return id.ToString("N");
                }
            }

            using (var streamWriter = File.CreateText(identifierFilePath))
            {
                var identifier = Guid.NewGuid().ToString("N");
                await streamWriter.WriteLineAsync(identifier);
                return identifier;
            }
        }
    }
}
