using System.Xml;
using Xunit;
using SvnBridge.Infrastructure;
using SvnBridge.Protocol;
using SvnBridge.Utility;

namespace UnitTests
{
    public class BrokenXmlTests
    {
        [Fact]
        public void Escape_CanEscapeBrokenXml()
        {
            string brokenXml = "<?xml version=\"1.0\" encoding=\"utf-8\" ?><D:propertyupdate xmlns:D=\"DAV:\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:C=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:S=\"http://subversion.tigris.org/xmlns/svn/\"><D:set><D:prop><C:bugtraq:label>Work Item:</C:bugtraq:label><C:bugtraq:url>http://www.codeplex.com/SvnBridge/WorkItem/View.aspx?WorkItemId=%BUGID%</C:bugtraq:url><C:bugtraq:message> Work Item: %BUGID%</C:bugtraq:message><C:bugtraq:number>true</C:bugtraq:number><C:bugtraq:warnifnoissue>true</C:bugtraq:warnifnoissue></D:prop></D:set></D:propertyupdate>";
            string validXml = BrokenXml.Escape(brokenXml);
            XmlDocument xdoc = new XmlDocument();
            xdoc.LoadXml(validXml);
        }

		[Fact]
        public void Escape_CanDeserializeBrokenXml()
		{
			string brokenXml = "<?xml version=\"1.0\" encoding=\"utf-8\" ?><D:propertyupdate xmlns:D=\"DAV:\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:C=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:S=\"http://subversion.tigris.org/xmlns/svn/\"><D:set><D:prop><C:bugtraq:label>Work Item:</C:bugtraq:label><C:bugtraq:url>http://www.codeplex.com/SvnBridge/WorkItem/View.aspx?WorkItemId=%BUGID%</C:bugtraq:url><C:bugtraq:message> Work Item: %BUGID%</C:bugtraq:message><C:bugtraq:number>true</C:bugtraq:number><C:bugtraq:warnifnoissue>true</C:bugtraq:warnifnoissue></D:prop></D:set></D:propertyupdate>";
			string validXml = BrokenXml.Escape(brokenXml);
			PropertyUpdateData xml = Helper.DeserializeXml<PropertyUpdateData>(validXml);
			Assert.Equal(5, xml.Set.Prop.Properties.Count);
		}

    	[Fact]
        public void Escape_CanEscapeBrokenXmlWhenUsingSingleTagOpenClose()
    	{
			string brokenXml = "<?xml version=\"1.0\" encoding=\"utf-8\" ?><D:propertyupdate xmlns:D=\"DAV:\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:C=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:S=\"http://subversion.tigris.org/xmlns/svn/\"><D:remove><D:prop><S:svn:ignore/></D:prop></D:remove></D:propertyupdate>";
			string validXml = BrokenXml.Escape(brokenXml);
			XmlDocument xdoc = new XmlDocument();
			xdoc.LoadXml(validXml);
    	}

        [Fact]
        public void UnEscape_RevertsToOriginalXml()
        {
            string brokenXml = "<?xml version=\"1.0\" encoding=\"utf-8\" ?><D:propertyupdate xmlns:D=\"DAV:\" xmlns:V=\"http://subversion.tigris.org/xmlns/dav/\" xmlns:C=\"http://subversion.tigris.org/xmlns/custom/\" xmlns:S=\"http://subversion.tigris.org/xmlns/svn/\"><D:set><D:prop><C:bugtraq:label>Work Item:</C:bugtraq:label><C:bugtraq:url>http://www.codeplex.com/SvnBridge/WorkItem/View.aspx?WorkItemId=%BUGID%</C:bugtraq:url><C:bugtraq:message> Work Item: %BUGID%</C:bugtraq:message><C:bugtraq:number>true</C:bugtraq:number><C:bugtraq:warnifnoissue>true</C:bugtraq:warnifnoissue></D:prop></D:set></D:propertyupdate>";
            string validXml = BrokenXml.Escape(brokenXml);

            string result = BrokenXml.UnEscape(validXml);

            Assert.Equal(brokenXml, result);
        }
    }
}