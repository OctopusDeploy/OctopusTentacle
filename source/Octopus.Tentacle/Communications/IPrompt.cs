namespace Octopus.Tentacle.Communications
{
    public interface IPrompt
    {
        bool CanPrompt { get; }

        bool Confirm(string context, string question);
    }
}
