namespace Octopus.Tentacle.Tests.Integration.Util.TcpUtils
{
    public class BiDirectionalDataTransferObserver
    {
        public BiDirectionalDataTransferObserver(IDataTransferObserver dataTransferObserverClientToOrigin, IDataTransferObserver dataTransferObserverOriginToClient)
        {
            DataTransferObserverClientToOrigin = dataTransferObserverClientToOrigin;
            DataTransferObserverOriginToClient = dataTransferObserverOriginToClient;
        }

        public IDataTransferObserver DataTransferObserverClientToOrigin { get; }
        public IDataTransferObserver DataTransferObserverOriginToClient { get; }
    }
}