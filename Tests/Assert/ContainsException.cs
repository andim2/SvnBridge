using System;
using System.Runtime.Serialization;

namespace CodePlex.NUnitExtensions
{
    [Serializable]
    public class ContainsException : AssertException
    {
        // Lifetime

        protected ContainsException(SerializationInfo info,
                                    StreamingContext context)
            : base(info, context)
        {
        }

        public ContainsException(object expected)
            : base(string.Format("Assert.Contains() failure: Not found: {0}", expected))
        {
        }
    }
}