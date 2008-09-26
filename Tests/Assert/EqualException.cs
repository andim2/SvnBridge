using System;
using System.Runtime.Serialization;

namespace CodePlex.NUnitExtensions
{
    [Serializable]
    public class EqualException : AssertActualExpectedException
    {
        // Lifetime

        public EqualException(object expected,
                              object actual,
                              string userMessage)
            : base(actual, expected, userMessage ?? "Assert.Equal() Failure")
        {
        }

        protected EqualException(SerializationInfo info,
                                 StreamingContext context)
            : base(info, context)
        {
        }
    }
}