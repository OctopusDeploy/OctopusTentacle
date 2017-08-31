using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Octopus.Shared;
using Octopus.Shared.Internals.Options;
using Octopus.Tentacle.Commands.OptionSets;

namespace Octopus.Tentacle.Tests.Commands.OptionSets
{
    [TestFixture]
    public class ApiEndpointOptionsFixture
    {
        [Test]
        [TestCase(null    , null  , null      , null)]
        [TestCase("server", null  , null      , null    , ExpectedException = typeof(ControlledFailureException), ExpectedMessage = "Please specify a username and password, or an Octopus API key. You can get an API key from the Octopus web portal. E.g., --apiKey=ABC1234")]
        [TestCase("server", "user", null      , null    , ExpectedException = typeof(ControlledFailureException), ExpectedMessage = "Please specify a password for the specified user account")]
        [TestCase("server", "user", "password", null)]
        [TestCase("server", null  , "password", null    , ExpectedException = typeof(ControlledFailureException), ExpectedMessage = "Please specify a username for the specified password")]
        [TestCase("server", "user", "password", "apikey", ExpectedException = typeof(ControlledFailureException), ExpectedMessage = "Please specify a username and password, or an Octopus API key - not both.")]
        [TestCase("server", "user", null      , "apikey", ExpectedException = typeof(ControlledFailureException), ExpectedMessage = "Please specify a username and password, or an Octopus API key - not both.")]
        [TestCase("server", null  , "password", "apikey", ExpectedException = typeof(ControlledFailureException), ExpectedMessage = "Please specify a username and password, or an Octopus API key - not both.")]
        [TestCase("server", null  , null      , "apikey")]
        [TestCase(null    , "user", null      , null    , ExpectedException = typeof(ControlledFailureException), ExpectedMessage = "Please specify a password for the specified user account")]
        [TestCase(null    , "user", "password", null    , ExpectedException = typeof(ControlledFailureException), ExpectedMessage = "Please specify an Octopus server, e.g., --server=http://your-octopus-server")]
        [TestCase(null    , null  , "password", null    , ExpectedException = typeof(ControlledFailureException), ExpectedMessage = "Please specify a username for the specified password")]
        [TestCase(null    , "user", "password", "apikey", ExpectedException = typeof(ControlledFailureException), ExpectedMessage = "Please specify a username and password, or an Octopus API key - not both.")]
        [TestCase(null    , "user", null      , "apikey", ExpectedException = typeof(ControlledFailureException), ExpectedMessage = "Please specify a username and password, or an Octopus API key - not both.")]
        [TestCase(null    , null  , "password", "apikey", ExpectedException = typeof(ControlledFailureException), ExpectedMessage = "Please specify a username and password, or an Octopus API key - not both.")]
        [TestCase(null    , null  , null      , "apikey", ExpectedException = typeof(ControlledFailureException), ExpectedMessage = "Please specify an Octopus server, e.g., --server=http://your-octopus-server")]
        public void ValidationIsCorrectWhenOptional(string server, string username, string password, string apiKey)
        {
            var optionSet = new OptionSet();
            var api = new ApiEndpointOptions(optionSet) { Optional = true };
            var args = new List<string>();
            if (!string.IsNullOrEmpty(server))
            {
                args.Add("--server");
                args.Add(server);
            }
            if (!string.IsNullOrEmpty(username))
            {
                args.Add("--username");
                args.Add(username);
            }
            if (!string.IsNullOrEmpty(password))
            {
                args.Add("--password");
                args.Add(password);
            }
            if (!string.IsNullOrEmpty(apiKey))
            {
                args.Add("--apikey");
                args.Add(apiKey);
            }
            optionSet.Parse(args);
            api.Validate();
        }

        [Test]
        [TestCase(null, null, null, null, ExpectedException = typeof(ControlledFailureException), ExpectedMessage = "Please specify an Octopus server, e.g., --server=http://your-octopus-server")]
        [TestCase("server", null, null, null, ExpectedException = typeof(ControlledFailureException), ExpectedMessage = "Please specify a username and password, or an Octopus API key. You can get an API key from the Octopus web portal. E.g., --apiKey=ABC1234")]
        [TestCase("server", "user", null, null, ExpectedException = typeof(ControlledFailureException), ExpectedMessage = "Please specify a password for the specified user account")]
        [TestCase("server", "user", "password", null)]
        [TestCase("server", null, "password", null, ExpectedException = typeof(ControlledFailureException), ExpectedMessage = "Please specify a username for the specified password")]
        [TestCase("server", "user", "password", "apikey", ExpectedException = typeof(ControlledFailureException), ExpectedMessage = "Please specify a username and password, or an Octopus API key - not both.")]
        [TestCase("server", "user", null, "apikey", ExpectedException = typeof(ControlledFailureException), ExpectedMessage = "Please specify a username and password, or an Octopus API key - not both.")]
        [TestCase("server", null, "password", "apikey", ExpectedException = typeof(ControlledFailureException), ExpectedMessage = "Please specify a username and password, or an Octopus API key - not both.")]
        [TestCase("server", null, null, "apikey")]
        [TestCase(null, "user", null, null, ExpectedException = typeof(ControlledFailureException), ExpectedMessage = "Please specify a password for the specified user account")]
        [TestCase(null, "user", "password", null, ExpectedException = typeof(ControlledFailureException), ExpectedMessage = "Please specify an Octopus server, e.g., --server=http://your-octopus-server")]
        [TestCase(null, null, "password", null, ExpectedException = typeof(ControlledFailureException), ExpectedMessage = "Please specify a username for the specified password")]
        [TestCase(null, "user", "password", "apikey", ExpectedException = typeof(ControlledFailureException), ExpectedMessage = "Please specify a username and password, or an Octopus API key - not both.")]
        [TestCase(null, "user", null, "apikey", ExpectedException = typeof(ControlledFailureException), ExpectedMessage = "Please specify a username and password, or an Octopus API key - not both.")]
        [TestCase(null, null, "password", "apikey", ExpectedException = typeof(ControlledFailureException), ExpectedMessage = "Please specify a username and password, or an Octopus API key - not both.")]
        [TestCase(null, null, null, "apikey", ExpectedException = typeof(ControlledFailureException), ExpectedMessage = "Please specify an Octopus server, e.g., --server=http://your-octopus-server")]
        public void ValidationIsCorrectWhenMandatory(string server, string username, string password, string apiKey)
        {
            var optionSet = new OptionSet();
            var api = new ApiEndpointOptions(optionSet) { Optional = false };
            var args = new List<string>();
            if (!string.IsNullOrEmpty(server))
            {
                args.Add("--server");
                args.Add(server);
            }
            if (!string.IsNullOrEmpty(username))
            {
                args.Add("--username");
                args.Add(username);
            }
            if (!string.IsNullOrEmpty(password))
            {
                args.Add("--password");
                args.Add(password);
            }
            if (!string.IsNullOrEmpty(apiKey))
            {
                args.Add("--apikey");
                args.Add(apiKey);
            }
            optionSet.Parse(args);
            api.Validate();
        }

    }
}
