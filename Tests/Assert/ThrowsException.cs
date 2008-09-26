using System;
using System.Runtime.Serialization;

namespace CodePlex.NUnitExtensions
{
    [Serializable]
    public class ThrowsException : AssertActualExpectedException
    {
        // Lifetime

        public ThrowsException(Type expectedType)
            : this("(No exception was thrown)", expectedType)
        {
        }

        public ThrowsException(Exception actual,
                               Type expectedType)
            : this(actual == null ? null : actual.GetType().FullName, expectedType)
        {
        }

        private ThrowsException(string expected,
                                Type actual)
            : base(expected, actual.FullName, "Assert.Throws() Failure")
        {
        }

        protected ThrowsException(SerializationInfo info,
                                  StreamingContext context)
            : base(info, context)
        {
        }
    }
}