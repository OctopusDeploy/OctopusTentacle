using System;
using System.IO;

namespace Octopus.Tentacle.Tests.Integration.Util.TcpUtils
{
    public class DataTransferObserverBuilder
    {
        private Action<MemoryStream> WritingDataObserver = stream => { };
        
        public DataTransferObserverBuilder WithWritingDataObserver(Action<MemoryStream> WritingDataObserver)
        {
            this.WritingDataObserver = WritingDataObserver;
            return this;
        }
        
        public IDataTransferObserver Build()
        {
            return new ActionDataTransferObserver(WritingDataObserver);
        }

        private class ActionDataTransferObserver : IDataTransferObserver
        {
            private Action<MemoryStream> WritingDataObserver;

            public ActionDataTransferObserver(Action<MemoryStream> writingDataObserver)
            {
                WritingDataObserver = writingDataObserver;
            }

            public void WritingData(MemoryStream buffer)
            {
                WritingDataObserver(buffer);
            }
        }
    }
}