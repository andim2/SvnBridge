using System;
using System.Runtime.Serialization;

namespace CodePlex.NUnitExtensions
{
    [Serializable]
    public class NotEqualException : AssertException
    {
        // Lifetime

        protected NotEqualException(SerializationInfo info,
                                    StreamingContext context)
            : base(info, context)
        {
        }

        public NotEqualException(string userMessage)
            : base(userMessage ?? "Assert.NotEqual() Failure")
        {
        }
    }
}