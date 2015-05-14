using System;
using System.Diagnostics;
using System.IO;

namespace Octopus.Shared.Util
{
    [DebuggerNonUserCode]
    public static class Guard
    {
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
                throw new ArgumentException(string.Format("The parameter '{0}' cannot be empty.", parameterName), parameterName);
            }
        }

        public static void FileExists(string argument)
        {
            if (!File.Exists(argument))
            {
                throw new ArgumentException(string.Format("Could not find file '{0}'", argument));
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
                throw new ArgumentException(string.Format("Cannot create directory, file exists '{0}'", argument));
            }
        }
    }
}
