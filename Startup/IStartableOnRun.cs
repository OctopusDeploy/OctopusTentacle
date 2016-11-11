namespace Octopus.Shared.Startup
{
    /// <summary>
    /// Instances of this instance will have their Start method called as the Run/RunAgent Command starts up.
    /// </summary>
    public interface IStartableOnRun
    {
        void Start();
    }
}