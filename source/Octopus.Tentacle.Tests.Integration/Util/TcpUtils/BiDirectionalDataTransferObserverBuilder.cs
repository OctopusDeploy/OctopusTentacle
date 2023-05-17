namespace Octopus.Tentacle.Tests.Integration.Util.TcpUtils
{
    public class BiDirectionalDataTransferObserverBuilder
    {
        IDataTransferObserver DataTransferObserverToRemote = new DataTransferObserverBuilder().Build();
        IDataTransferObserver DataTransferObserverFromRemote = new DataTransferObserverBuilder().Build();

        public BiDirectionalDataTransferObserverBuilder ObserveDataToRemote(IDataTransferObserver DataTransferObserverToRemote)
        {
            this.DataTransferObserverToRemote = DataTransferObserverToRemote;
            return this;
        }
        
        public BiDirectionalDataTransferObserverBuilder ObserveDataFromRemote(IDataTransferObserver DataTransferObserverFromRemote)
        {
            this.DataTransferObserverFromRemote = DataTransferObserverFromRemote;
            return this;
        }

        public BiDirectionalDataTransferObserver Build()
        {
            return new BiDirectionalDataTransferObserver(DataTransferObserverToRemote, DataTransferObserverFromRemote);
        }
        
    }
}