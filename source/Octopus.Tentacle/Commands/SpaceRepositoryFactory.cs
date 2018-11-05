using System;
using System.Threading.Tasks;
using Octopus.Client;
using Octopus.Shared;
using Octopus.Shared.Properties;

namespace Octopus.Tentacle.Commands
{
    public interface ISpaceRepositoryFactory
    {
        Task<IOctopusSpaceAsyncRepository> CreateSpaceRepository(IOctopusAsyncClient client, [CanBeNull]string spaceName);
    }

    public class SpaceRepositoryFactory : ISpaceRepositoryFactory
    {
        public async Task<IOctopusSpaceAsyncRepository> CreateSpaceRepository(IOctopusAsyncClient client, [CanBeNull]string spaceName)
        {
            if (!string.IsNullOrEmpty(spaceName))
            {
                if (!await SupportsSpaces(client))
                {
                    throw new ControlledFailureException($"A space with name \"{spaceName}\" was requested, but this version of Octopus Server does not support spaces. Please upgrade Octopus Server to a version which supports spaces, or remove the \"space\" parameter.");
                }

                var space = await client.Repository.Spaces.FindByName(spaceName);
                if (space == null)
                {
                    throw new ControlledFailureException($"A space with name \"{spaceName}\" could not be found. Ensure you have spelled the space name correctly and that the user has access to this space");
                }

                return client.ForSpace(space.Id);
            }

            if (await SupportsSpaces(client))
            {
                var defaultSpace = await client.Repository.Spaces.FindOne(s => s.IsDefault);
                if (defaultSpace == null)
                {
                    throw new ControlledFailureException("No \"space\" was specified, and the default space is disabled or inaccessible to this user. Please select a space using the \"space\" parameter.");
                }

                return client.ForSpace(defaultSpace.Id);
            }

            // This works in a backwards compatible way for the endpoints that tentacle cares about
            return client.Repository;
        }

        Task<bool> SupportsSpaces(IOctopusAsyncClient client)
        {
            return client.Repository.HasLink("Spaces");
        }
    }
}