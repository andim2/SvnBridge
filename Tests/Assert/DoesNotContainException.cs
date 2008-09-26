using System;
using System.Runtime.Serialization;

namespace CodePlex.NUnitExtensions
{
    [Serializable]
    public class DoesNotContainException : AssertException
    {
        // Lifetime

        protected DoesNotContainException(SerializationInfo info,
                                          StreamingContext context)
            : base(info, context)
        {
        }

        public DoesNotContainException(object expected)
            : base(string.Format("Assert.DoesNotContain() failure: Found: {0}", expected))
        {
        }
    }
}