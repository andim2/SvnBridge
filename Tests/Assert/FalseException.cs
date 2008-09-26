using System;
using System.Runtime.Serialization;

namespace CodePlex.NUnitExtensions
{
    [Serializable]
    public class FalseException : AssertException
    {
        // Lifetime

        protected FalseException(SerializationInfo info,
                                 StreamingContext context)
            : base(info, context)
        {
        }

        public FalseException(string userMessage)
            : base(userMessage ?? "Assert.False() Failure")
        {
        }
    }
}