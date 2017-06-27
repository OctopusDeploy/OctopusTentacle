using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Octopus.Shared.Util
{
    [DebuggerNonUserCode]
    public static class Guard
    {
        static readonly Lazy<List<string>> specialLocations = new Lazy<List<string>>(() =>
        {
            var result = new List<string>();
            foreach (var specialLocation in Enum.GetValues(typeof (Environment.SpecialFolder)))
            {
                var location = Environment.GetFolderPath((Environment.SpecialFolder)specialLocation, Environment.SpecialFolderOption.None);
                result.Add(location);
            }
            result.Add("C:");
            result.Add("C:\\");
            return result;
        });

        public static void ArgumentNotNull(object argument, string parameterName)
        {
            if (argument == null)
                throw new ArgumentNullException(parameterName);
        }

        public static void ArgumentIsOfType(object argument, Type type, string parameterName)
        {
            if (argument == null || !type.IsInstanceOfType(argument))
                throw new ArgumentException(parameterName);
        }

        public static void ArgumentNotNullOrEmpty(string argument, string parameterName)
        {
            ArgumentNotNull(argument, parameterName);
            if (argument.Trim().Length == 0)
            {
                throw new ArgumentException($"The parameter '{parameterName}' cannot be empty.", parameterName);
            }
        }

        /// <summary>
        /// Throw if a file does not exist
        /// </summary>
        /// <param name="argument">file path</param>
        /// <param name="errorMessage">Optional error message to replace the default could not find file</param>
        public static void FileExists(string argument, string errorMessage = null)
        {
            if (!File.Exists(argument))
            {
                if (string.IsNullOrWhiteSpace(errorMessage))
                    throw new FileNotFoundException(errorMessage);
                throw new FileNotFoundException($"Could not find file '{argument}'");
            }
        }

        /// <summary>
        /// Throws if a file exists
        /// </summary>
        /// <remarks>
        /// Use this when the argument is a directory and you need to be sure they don't enter an existing filename
        /// </remarks>
        public static void FileDoesNotExist(string argument)
        {
            if (File.Exists(argument))
            {
                throw new ArgumentException($"Cannot create directory, file exists '{argument}'");
            }
        }

        /// <summary>
        /// Throws if a directory does not exist
        /// </summary>
        /// <remarks>
        /// Use this when the argument is a directory and you need to be sure that it is present
        /// </remarks>
        public static void DirectoryExists(string argument)
        {
            if (!Directory.Exists(argument))
            {
                throw new DirectoryNotFoundException($"Directory '{argument}' not found");
            }
        }

        /// <summary>
        /// Throws if a directory has been specified that is not a good place to store files
        /// </summary>
        /// <remarks>
        /// Use this to make sure the user doesn't export to somewhere stupid
        /// </remarks>
        public static void ArgumentNotSpecialLocation(string argument)
        {
            foreach (var path in specialLocations.Value)
            {
                if (string.Equals(argument, path, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException($"Directory '{argument}' is not a good place, pick a safe subdirectory");
            }
        }

        public static void ArgumentNotNegativeValue(long argumentValue, string argumentName)
        {
            if (argumentValue < 0)
                throw new ArgumentOutOfRangeException(argumentName, $"Argument {argumentName} cannot be negative, but was: {argumentValue}");
        }

        public static void ArgumentNotGreaterThan(double argumentValue, double ceilingValue, string argumentName)
        {
            if (argumentValue > ceilingValue)
                throw new ArgumentOutOfRangeException(argumentName, $"Argument {argumentName} cannot be greater than {ceilingValue} but was {argumentValue}");
        }
    }
}