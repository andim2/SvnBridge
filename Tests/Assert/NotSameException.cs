using System;
using System.Runtime.Serialization;

namespace CodePlex.NUnitExtensions
{
    [Serializable]
    public class NotSameException : AssertException
    {
        // Lifetime

        protected NotSameException(SerializationInfo info,
                                   StreamingContext context)
            : base(info, context)
        {
        }

        public NotSameException(string userMessage)
            : base(userMessage ?? "Assert.NotSame() Failure")
        {
        }
    }
}