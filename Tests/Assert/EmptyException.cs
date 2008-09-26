using System;
using System.Runtime.Serialization;

namespace CodePlex.NUnitExtensions
{
    [Serializable]
    public class EmptyException : AssertException
    {
        // Lifetime

        protected EmptyException(SerializationInfo info,
                                 StreamingContext context)
            : base(info, context)
        {
        }

        public EmptyException()
            : base(string.Format("Assert.Empty() failure"))
        {
        }
    }
}