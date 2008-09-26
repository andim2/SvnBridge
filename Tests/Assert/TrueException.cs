using System;
using System.Runtime.Serialization;

namespace CodePlex.NUnitExtensions
{
    [Serializable]
    public class TrueException : AssertException
    {
        // Lifetime

        protected TrueException(SerializationInfo info,
                                StreamingContext context)
            : base(info, context)
        {
        }

        public TrueException(string userMessage)
            : base(userMessage ?? "Assert.True() Failure")
        {
        }
    }
}