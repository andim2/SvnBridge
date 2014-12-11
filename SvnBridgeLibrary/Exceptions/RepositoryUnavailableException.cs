using System;
using System.Runtime.Serialization;

namespace SvnBridge.Exceptions
{
    [Serializable]
    public class RepositoryUnavailableException : Exception
    {
        //
        // For guidelines regarding the creation of new exception types, see
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/cpgenref/html/cpconerrorraisinghandlingguidelines.asp
        // and
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dncscol/html/csharp07192001.asp
        //

        public RepositoryUnavailableException()
        {
        }

        public RepositoryUnavailableException(string message) : base(message)
        {
        }

        public RepositoryUnavailableException(string message,
                                              Exception inner) : base(message, inner)
        {
        }

        protected RepositoryUnavailableException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
        }
    }
}