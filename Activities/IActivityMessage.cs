namespace Octopus.Shared.Activities
{
    public interface IActivityMessage
    {
        string Name { get; set; }
        string Tag { get; set; }
    }
}