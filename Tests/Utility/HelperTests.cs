using Xunit;
using SvnBridge.Utility;

namespace UnitTests
{
    public class HelperTests
    {
        [Fact]
        public void VerifyDecodeBCorrectlyDecodesSpecialCharacters()
        {
            string result = Helper.Decode("&amp;");

            Assert.Equal("&", result);
        }

        [Fact]
        public void VerifyDecodeCorrectlyDecodesSpecialCharacters()
        {
            string result = Helper.Decode("%20%25%23%5e%7b%5b%7d%5d%3b%60&amp;");

            Assert.Equal(" %#^{[}];`&", result);
        }

        [Fact]
        public void VerifyDecodeWorksCorrectlyWithSpacesAndPercentages()
        {
            string result = Helper.Decode("%2520");

            Assert.Equal("%20", result);
        }

        [Fact]
        public void VerifyEncodeBCorrectlyEncodesSpecialCharacters()
        {
            string result = Helper.Encode("&");

            Assert.Equal("&amp;", result);
        }

        [Fact]
        public void VerifyEncodeCCorrectlyEncodesString()
        {
            string result = Helper.EncodeC(" %#^{[}];`");

            Assert.Equal("%20%25%23%5E%7B%5B%7D%5D%3B%60", result);
        }

        [Fact]
        public void VerifyEncodeCDoesNotEncodeAmpersand()
        {
            string result = Helper.EncodeC("&");

            Assert.Equal("&", result);
        }

        [Fact]
        public void VerifyEncodeCorrectlyEncodesSpecialCharacters()
        {
            string result = Helper.Encode(" %#^{[}];`&");

            Assert.Equal("%20%25%23%5e%7b%5b%7d%5d%3b%60&amp;", result);
        }

        [Fact]
        public void VerifyEncodeWithCapitalizeCorrectlyEncodesUsingCapitals()
        {
            string result = Helper.Encode(" %#^{[}];`", true);

            Assert.Equal("%20%25%23%5E%7B%5B%7D%5D%3B%60", result);
        }

        [Fact]
        public void VerifyEncodeWithCapitalizeDoesNotCapitalizeAmpersandEncoding()
        {
            string result = Helper.Encode("&", true);

            Assert.Equal("&amp;", result);
        }

        [Fact]
        public void VerifyEncodeWorksCorrectlyWithSpacesAndPercentages()
        {
            string result = Helper.Encode(" % %");

            Assert.Equal("%20%25%20%25", result);
        }

        /// <remarks>
        /// Candidate contains both multi-byte-sequence chars *and* single-byte ones ("Ä").
        /// </remarks>
        [Fact]
        [Trait("TestName", "VDWCWNSBCR")]
        public void VerifyDecodeWorksCorrectlyWithNonSingleByteCharRange()
        {
            string result = Helper.Decode("%e2%82%Ac%e6%B5%8b%e8%Af%95" + "%c3%84%c3%b6%c3%bC");

            Assert.Equal("€测试" + "Äöü", result);
        }

        [Fact]
        [Trait("TestName", "VEWCWNSBCR")]
        public void VerifyEncodeWorksCorrectlyWithNonSingleByteCharRange()
        {
            string result = Helper.Encode("€测试" + "Äöü");

            Assert.Equal("%e2%82%ac%e6%b5%8b%e8%af%95" + "%c3%84%c3%b6%c3%bc", result);
        }

        [Fact]
        public void SerializeXmlString_CorrectlyReturnsSerializedObject()
        {
            string result = Helper.SerializeXmlString("Hello");

            Assert.Equal("<?xml version=\"1.0\" encoding=\"utf-16\"?>\r\n<string>Hello</string>", result);
        }
    }
}
