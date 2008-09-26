using System;
using System.Runtime.Serialization;

namespace CodePlex.NUnitExtensions
{
    [Serializable]
    public class SameException : AssertActualExpectedException
    {
        // Lifetime

        public SameException(object expected,
                             object actual,
                             string userMessage)
            : base(actual, expected, userMessage ?? "Assert.Same() Failure")
        {
        }

        protected SameException(SerializationInfo info,
                                StreamingContext context)
            : base(info, context)
        {
        }
    }
}