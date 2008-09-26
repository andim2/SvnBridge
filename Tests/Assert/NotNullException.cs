using System;
using System.Runtime.Serialization;

namespace CodePlex.NUnitExtensions
{
    [Serializable]
    public class NotNullException : AssertException
    {
        // Lifetime

        protected NotNullException(SerializationInfo info,
                                   StreamingContext context)
            : base(info, context)
        {
        }

        public NotNullException(string userMessage)
            : base(userMessage ?? "Assert.NotNull() Failure")
        {
        }
    }
}