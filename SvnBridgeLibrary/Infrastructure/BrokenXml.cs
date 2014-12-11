using System.Text.RegularExpressions;

namespace SvnBridge.Infrastructure
{
    /// <summary>
    /// This is a sadly needed class, because SVN has properties that uses a colon in their names,
    /// and will send that as element names in the XML request for PROPPATCH.
    /// This is not legal XML, and cause issues.
    /// We work around that problem by escaping the colon to __COLON__ and unescaping it when we write back
    /// to the client.
    /// </summary>
    public class BrokenXml
    {
        static readonly Regex findDuplicateNamespacesInTagStart = new Regex(@"<\s*([\w\d]+):([\w\d]+):([\w\d]+)\s*>", RegexOptions.Compiled);
		static readonly Regex findDuplicateNamespacesInTag = new Regex(@"<\s*([\w\d]+):([\w\d]+):([\w\d]+)\s*/>", RegexOptions.Compiled);
        static readonly Regex findDuplicateNamespacesInTagEnd = new Regex(@"</\s*([\w\d]+):([\w\d]+):([\w\d]+)\s*>", RegexOptions.Compiled);

        public static string Escape(string brokenXml)
        {
            string replaced = findDuplicateNamespacesInTagStart.Replace(brokenXml, "<$1:$2__COLON__$3>");
        	replaced = findDuplicateNamespacesInTag.Replace(replaced, "<$1:$2__COLON__$3/>");
            return findDuplicateNamespacesInTagEnd.Replace(replaced, "</$1:$2__COLON__$3>");
        }

        public static string UnEscape(string brokenXml)
        {
            return brokenXml.Replace("__COLON__", ":");
        }
    }
}