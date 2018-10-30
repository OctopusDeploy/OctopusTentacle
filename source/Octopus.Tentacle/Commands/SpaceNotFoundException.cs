using System;

namespace Octopus.Tentacle.Commands
{
    class SpaceNotFoundException : Exception
    {
        public SpaceNotFoundException(string spaceName) 
            : base($"A space with name \"{spaceName}\" could not be found. Ensure you have spelled the space name correctly and that the user has access to this space")
        {
        }
    }
}