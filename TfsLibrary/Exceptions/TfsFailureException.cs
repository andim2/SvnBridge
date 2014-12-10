using System;

namespace CodePlex.TfsLibrary
{
    public class TfsFailureException : Exception
    {
        public TfsFailureException(string message)
            : base(message) {}
    }
}