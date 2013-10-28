using System;

namespace SvnBridge.SourceControl
{
    [Serializable]
    public class FutureInvocationException : Exception
    {
        public FutureInvocationException()
        {
        }

        public FutureInvocationException(string message) : base(message)
        {
        }

        public FutureInvocationException(string message, Exception inner) : base(message, inner)
        {
        }

        protected FutureInvocationException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }
    }
}