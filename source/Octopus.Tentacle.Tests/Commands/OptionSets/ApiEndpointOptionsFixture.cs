using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Internals.Options;

namespace Octopus.Tentacle.Tests.Commands.OptionSets
{
    [TestFixture]
    public class ApiEndpointOptionsFixture
    {
        [Test]
        [TestCase(null    , null  , null      , null    , null   , null)]
        [TestCase("server", null  , null      , null    , null   , "Please specify an Octopus API key, a Bearer Token or a username and password. You can get an API key from the Octopus web portal. E.g., --apiKey=ABC1234")]
        [TestCase("server", "user", null      , null    , null   , "Please specify a password for the specified user account")]
        [TestCase("server", "user", "password", null    , null   , null)]
        [TestCase("server", null  , "password", null    , null   , "Please specify a username for the specified password")]
        [TestCase("server", "user", "password", "apikey", null   , "Please specify a Bearer Token, API Key or username and password - not multiple.")]
        [TestCase("server", "user", null      , "apikey", null   , "Please specify a Bearer Token, API Key or username and password - not multiple.")]
        [TestCase("server", null  , "password", "apikey", null   , "Please specify a Bearer Token, API Key or username and password - not multiple.")]
        [TestCase("server", null  , null, "apikey"      , "token", "Please specify a Bearer Token, API Key or username and password - not multiple.")]
        [TestCase("server", null  , "password", null    , "token", "Please specify a Bearer Token, API Key or username and password - not multiple.")]
        [TestCase("server", "user", "password", null    , "token", "Please specify a Bearer Token, API Key or username and password - not multiple.")]
        [TestCase("server", "user", "password", "apikey", "token", "Please specify a Bearer Token, API Key or username and password - not multiple.")]
        [TestCase("server", null  , null      , "apikey", null   , null)]
        [TestCase(null    , "user", null      , null    , null   , "Please specify a password for the specified user account")]
        [TestCase(null    , "user", "password", null    , null   , "Please specify an Octopus Server, e.g., --server=http://your-octopus-server")]
        [TestCase(null    , null  , "password", null    , null   , "Please specify a username for the specified password")]
        [TestCase(null    , "user", "password", "apikey", null   , "Please specify a Bearer Token, API Key or username and password - not multiple.")]
        [TestCase(null    , "user", null      , "apikey", null   , "Please specify a Bearer Token, API Key or username and password - not multiple.")]
        [TestCase(null    , null  , "password", "apikey", null   , "Please specify a Bearer Token, API Key or username and password - not multiple.")]
        [TestCase(null    , null  , null      , "apikey", "token", "Please specify a Bearer Token, API Key or username and password - not multiple.")]
        [TestCase(null    , null  , null      , "apikey", null   , "Please specify an Octopus Server, e.g., --server=http://your-octopus-server")]
        [TestCase(null    , null  , null      , null    , "token", "Please specify an Octopus Server, e.g., --server=http://your-octopus-server")]
        public void ValidationIsCorrectWhenOptional(string server, string username, string password, string apiKey, string bearerToken, string expectedExceptionMessage)
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
            if (!string.IsNullOrEmpty(bearerToken))
            {
                args.Add("--bearerToken");
                args.Add(bearerToken);
            }
            optionSet.Parse(args);

            if (expectedExceptionMessage == null)
            {
                api.Validate();
            }
            else
            {
                Action action = api.Validate;
                action.Should().Throw<ControlledFailureException>().WithMessage(expectedExceptionMessage);
            }
        }

        [Test]
        [TestCase(null    , null  , null        , null    , null   , "Please specify an Octopus Server, e.g., --server=http://your-octopus-server")]
        [TestCase("server", null  , null        , null    , null   , "Please specify an Octopus API key, a Bearer Token or a username and password. You can get an API key from the Octopus web portal. E.g., --apiKey=ABC1234")]
        [TestCase("server", "user", null        , null    , null   , "Please specify a password for the specified user account")]
        [TestCase("server", "user", "password"  , null    , null   , null)]
        [TestCase("server", null  , "password"  , null    , null   , "Please specify a username for the specified password")]
        [TestCase("server", "user", "password"  , "apikey", null   , "Please specify a Bearer Token, API Key or username and password - not multiple.")]
        [TestCase("server", "user", null        , "apikey", null   , "Please specify a Bearer Token, API Key or username and password - not multiple.")]
        [TestCase("server", null  , "password"  , "apikey", null   , "Please specify a Bearer Token, API Key or username and password - not multiple.")]
        [TestCase("server", null  , null        , "apikey", "token", "Please specify a Bearer Token, API Key or username and password - not multiple.")]
        [TestCase("server", null  , "password"  , null    , "token", "Please specify a Bearer Token, API Key or username and password - not multiple.")]
        [TestCase("server", "user", "password"  , null    , "token", "Please specify a Bearer Token, API Key or username and password - not multiple.")]
        [TestCase("server", "user", "password"  , "apikey", "token", "Please specify a Bearer Token, API Key or username and password - not multiple.")]
        [TestCase("server", null  , null        , "apikey", null   , null)]
        [TestCase("server", null  , null        , null    , "token", null)]
        [TestCase(null    , "user", null        , null    , null   , "Please specify a password for the specified user account")]
        [TestCase(null    , "user", "password"  , null    , null   , "Please specify an Octopus Server, e.g., --server=http://your-octopus-server")]
        [TestCase(null    , null  , "password"  , null    , null   , "Please specify a username for the specified password")]
        [TestCase(null    , "user", "password"  , "apikey", null   , "Please specify a Bearer Token, API Key or username and password - not multiple.")]
        [TestCase(null    , "user", null        , "apikey", null   , "Please specify a Bearer Token, API Key or username and password - not multiple.")]
        [TestCase(null    , null  , "password"  , "apikey", null   , "Please specify a Bearer Token, API Key or username and password - not multiple.")]
        [TestCase(null    , null  , null        , "apikey", null   , "Please specify an Octopus Server, e.g., --server=http://your-octopus-server")]
        public void ValidationIsCorrectWhenMandatory(string server, string username, string password, string apiKey, string bearerToken, string expectedExceptionMessage)
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
            if (!string.IsNullOrEmpty(bearerToken))
            {
                args.Add("--bearerToken");
                args.Add(bearerToken);
            }
            optionSet.Parse(args);

            if (expectedExceptionMessage == null)
            {
                api.Validate();
            }
            else
            {
                Action action = api.Validate;
                action.Should().Throw<ControlledFailureException>().WithMessage(expectedExceptionMessage);
            }
        }
    }
}
