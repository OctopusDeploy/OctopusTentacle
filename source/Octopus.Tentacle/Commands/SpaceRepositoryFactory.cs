using System;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Client;
using Octopus.Client.Model;
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

                var userSpaces = await FetchUserSpaces();
                var space = userSpaces.SingleOrDefault(s => string.Equals(s.Name.Trim(), spaceName, StringComparison.OrdinalIgnoreCase));
                if (space == null)
                {
                    throw new ControlledFailureException($"A space with name \"{spaceName}\" could not be found. Ensure you have spelled the space name correctly and that the user has access to this space.");
                }

                return client.ForSpace(space);
            }

            if (await SupportsSpaces(client))
            {
                var userSpaces = await FetchUserSpaces();
                var defaultSpace = userSpaces.SingleOrDefault(s => s.IsDefault);
                if (defaultSpace == null)
                {
                    throw new ControlledFailureException("No \"space\" was specified, and the default space is disabled or inaccessible to this user. Please select a space using the \"space\" parameter.");
                }

                return client.ForSpace(defaultSpace);
            }

            // This works in a backwards compatible way for the endpoints that tentacle cares about
            return client.Repository;

            // We only want to return the spaces within which this user has access, rather than the spaces that this user can see.
            // This means if a user can see a space because they have the SpaceView permission, they still might not have access within that specific space
            // This allows us to fail earlier in that scenario with a more helpful message.
            async Task<SpaceResource[]> FetchUserSpaces()
            {
                var userRepository = client.ForSystem().Users;
                var currentUser = await userRepository.GetCurrent();
                return await userRepository.GetSpaces(currentUser);
            }
        }

        Task<bool> SupportsSpaces(IOctopusAsyncClient client)
        {
            return client.Repository.HasLink("Spaces");
        }
    }
}
