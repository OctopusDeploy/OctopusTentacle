using System;
using System.Collections.Generic;
using FluentAssertions;
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
        [TestCase(null, null, null, null, null)]
        [TestCase("server", null, null, null, "Please specify a username and password, or an Octopus API key. You can get an API key from the Octopus web portal. E.g., --apiKey=ABC1234")]
        [TestCase("server", "user", null, null, "Please specify a password for the specified user account")]
        [TestCase("server", "user", "password", null, null)]
        [TestCase("server", null, "password", null, "Please specify a username for the specified password")]
        [TestCase("server", "user", "password", "apikey", "Please specify a username and password, or an Octopus API key - not both.")]
        [TestCase("server", "user", null, "apikey", "Please specify a username and password, or an Octopus API key - not both.")]
        [TestCase("server", null, "password", "apikey", "Please specify a username and password, or an Octopus API key - not both.")]
        [TestCase("server", null, null, "apikey", null)]
        [TestCase(null, "user", null, null, "Please specify a password for the specified user account")]
        [TestCase(null, "user", "password", null, "Please specify an Octopus Server, e.g., --server=http://your-octopus-server")]
        [TestCase(null, null, "password", null, "Please specify a username for the specified password")]
        [TestCase(null, "user", "password", "apikey", "Please specify a username and password, or an Octopus API key - not both.")]
        [TestCase(null, "user", null, "apikey", "Please specify a username and password, or an Octopus API key - not both.")]
        [TestCase(null, null, "password", "apikey", "Please specify a username and password, or an Octopus API key - not both.")]
        [TestCase(null, null, null, "apikey", "Please specify an Octopus Server, e.g., --server=http://your-octopus-server")]
        public void ValidationIsCorrectWhenOptional(string server, string username, string password, string apiKey, string expectedExceptionMessage)
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
        [TestCase(null, null, null, null, "Please specify an Octopus Server, e.g., --server=http://your-octopus-server")]
        [TestCase("server", null, null, null, "Please specify a username and password, or an Octopus API key. You can get an API key from the Octopus web portal. E.g., --apiKey=ABC1234")]
        [TestCase("server", "user", null, null, "Please specify a password for the specified user account")]
        [TestCase("server", "user", "password", null, null)]
        [TestCase("server", null, "password", null, "Please specify a username for the specified password")]
        [TestCase("server", "user", "password", "apikey", "Please specify a username and password, or an Octopus API key - not both.")]
        [TestCase("server", "user", null, "apikey", "Please specify a username and password, or an Octopus API key - not both.")]
        [TestCase("server", null, "password", "apikey", "Please specify a username and password, or an Octopus API key - not both.")]
        [TestCase("server", null, null, "apikey", null)]
        [TestCase(null, "user", null, null, "Please specify a password for the specified user account")]
        [TestCase(null, "user", "password", null, "Please specify an Octopus Server, e.g., --server=http://your-octopus-server")]
        [TestCase(null, null, "password", null, "Please specify a username for the specified password")]
        [TestCase(null, "user", "password", "apikey", "Please specify a username and password, or an Octopus API key - not both.")]
        [TestCase(null, "user", null, "apikey", "Please specify a username and password, or an Octopus API key - not both.")]
        [TestCase(null, null, "password", "apikey", "Please specify a username and password, or an Octopus API key - not both.")]
        [TestCase(null, null, null, "apikey", "Please specify an Octopus Server, e.g., --server=http://your-octopus-server")]
        public void ValidationIsCorrectWhenMandatory(string server, string username, string password, string apiKey, string expectedExceptionMessage)
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