using System;
using System.Runtime.Serialization;

namespace CodePlex.NUnitExtensions
{
    [Serializable]
    public class NullException : AssertActualExpectedException
    {
        // Lifetime

        public NullException(object actual,
                             string userMessage)
            : base(actual, null, userMessage ?? "Assert.Null() Failure")
        {
        }

        protected NullException(SerializationInfo info,
                                StreamingContext context)
            : base(info, context)
        {
        }
    }
}