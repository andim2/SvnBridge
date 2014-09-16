using System;

namespace CodePlex.TfsLibrary
{
    public static class Guard
    {
        public static void ArgumentNotNull(object value,
                                           string argumentName)
        {
            if (value == null)
                throw new ArgumentNullException(argumentName);
        }

        public static void ArgumentNotNullOrEmpty(string value,
                                                  string argumentName)
        {
            ArgumentNotNull(value, argumentName);

            if (value == string.Empty)
                throw new ArgumentException("Empty string is not valid.", argumentName);
        }

        public static void ArgumentValid(bool test,
                                         string message)
        {
            if (!test)
                throw new ArgumentException(message);
        }
    }
}