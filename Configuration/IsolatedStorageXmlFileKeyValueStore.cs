using System;
using System.IO;
using System.IO.IsolatedStorage;

namespace Octopus.Shared.Configuration
{
    public class IsolatedStorageXmlFileKeyValueStore : XmlKeyValueStore, IDisposable
    {
        readonly string isolatedStorageFileName;
        readonly IsolatedStorageFile isoStore;

        public IsolatedStorageXmlFileKeyValueStore(string isolatedStorageFileName) : base(autoSaveOnSet:true)
        {
            this.isolatedStorageFileName = isolatedStorageFileName;

            isoStore = IsolatedStorageFile.GetUserStoreForDomain();
        }

        protected override bool ExistsForReading()
        {
            return isoStore.FileExists(isolatedStorageFileName);
        }

        protected override Stream OpenForReading()
        {
            return new IsolatedStorageFileStream(isolatedStorageFileName, FileMode.Open, FileAccess.Read, isoStore);
        }

        protected override Stream OpenForWriting()
        {
            return new IsolatedStorageFileStream(isolatedStorageFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, isoStore);
        }

        public void Dispose()
        {
            isoStore.Dispose();
        }
    }
}