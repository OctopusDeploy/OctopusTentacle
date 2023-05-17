using System.IO;

namespace Octopus.Tentacle.Tests.Integration.Util.TcpUtils
{
    public interface IDataTransferObserver
    {
        public void WritingData(TcpPump tcpPump, MemoryStream buffer);
    }
}