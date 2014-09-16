using System;

namespace CodePlex.TfsLibrary
{
    public class NetworkAccessDeniedException : Exception
    {
        public NetworkAccessDeniedException()
            : this(null) {}

        public NetworkAccessDeniedException(Exception innerException)
            : base("Access to the network resource is denied.", innerException) {}
    }
}