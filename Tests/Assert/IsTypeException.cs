using System;
using System.Runtime.Serialization;

namespace CodePlex.NUnitExtensions
{
    [Serializable]
    public class IsTypeException : AssertActualExpectedException
    {
        // Lifetime

        public IsTypeException(Type expected,
                               object actual,
                               string userMessage)
            : base(actual == null ? null : actual.GetType().FullName,
                   expected.FullName,
                   userMessage ?? "Assert.IsType() Failure")
        {
        }

        protected IsTypeException(SerializationInfo info,
                                  StreamingContext context)
            : base(info, context)
        {
        }
    }
}