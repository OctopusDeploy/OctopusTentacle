using System.IO;

namespace Octopus.Tentacle.Tests.Integration.Util.TcpUtils
{
    public interface IDataTransferObserver
    {
        public void WritingData(TcpPump tcpPump, MemoryStream buffer);

        public static IDataTransferObserver Combine(params IDataTransferObserver[] dataTransferObservers)
        {
            var builder = new DataTransferObserverBuilder();
            foreach (var dataTransferObserver in dataTransferObservers)
            {
                builder.WithWritingDataObserver(dataTransferObserver.WritingData);
            }

            return builder.Build();
        }
    }
}