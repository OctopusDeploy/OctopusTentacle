using System;

namespace Octopus.Tentacle.Client.Services
{
    public abstract class SyncAndAsyncService<TSync, TAsync>
    {
        private readonly TSync? syncService;
        private readonly TAsync? asyncService;

        public TSync SyncService
        {
            get
            {
                if (syncService == null)
                {
                    throw new ArgumentNullException(nameof(SyncService));
                }

                return syncService;
            }
        }
        
        public TAsync AsyncService
        {
            get
            {
                if (asyncService == null)
                {
                    throw new ArgumentNullException(nameof(AsyncService));
                }

                return asyncService;
            }
        }

        public SyncAndAsyncService(TSync? syncService, TAsync? asyncService)
        {
            this.syncService = syncService;
            this.asyncService = asyncService;
        }
    }
}
