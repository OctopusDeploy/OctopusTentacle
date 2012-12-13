namespace Octopus.Shared.Activities
{
    public interface IActivityResolver
    {
        object Locate(IActivityMessage message);
    }
}