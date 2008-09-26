using System.Runtime.Serialization;

namespace CodePlex.NUnitExtensions
{
    public class AssertActualExpectedException : AssertException
    {
        // Fields

        private string actual;
        private string expected;

        // Lifetime

        protected AssertActualExpectedException(SerializationInfo info,
                                                StreamingContext context)
            : base(info, context)
        {
            expected = info.GetString("Expected");
            actual = info.GetString("Actual");
        }

        public AssertActualExpectedException(object actual,
                                             object expected,
                                             string userMessage)
            : base(userMessage)
        {
            this.expected = expected == null ? null : expected.ToString();
            this.actual = actual == null ? null : actual.ToString();
        }

        // Properties

        public string Actual
        {
            get { return actual; }
        }

        public string Expected
        {
            get { return expected; }
        }

        public override string Message
        {
            get
            {
                return string.Format("{0}\r\nExpected: {1}\r\nActual:   {2}",
                                     base.Message,
                                     Expected ?? "(null)",
                                     Actual ?? "(null)");
            }
        }

        // Methods

        public override void GetObjectData(SerializationInfo info,
                                           StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue("Expected", expected);
            info.AddValue("Actual", actual);
        }
    }
}