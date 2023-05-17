namespace Octopus.Tentacle.Tests.Integration.Util.TcpUtils
{
    public class BiDirectionalDataTransferObserver
    {
        public BiDirectionalDataTransferObserver(IDataTransferObserver dataTransferObserverToRemote, IDataTransferObserver dataTransferObserverFromRemote)
        {
            DataTransferObserverToRemote = dataTransferObserverToRemote;
            DataTransferObserverFromRemote = dataTransferObserverFromRemote;
        }

        public IDataTransferObserver DataTransferObserverToRemote { get; }
        public IDataTransferObserver DataTransferObserverFromRemote { get; }
    }
}