using System;

namespace Octopus.Shared.Integration.Ftp
{
    public interface IFtpSynchronizer
    {
        void Synchronize(FtpSynchronizationSettings settings);
    }
}