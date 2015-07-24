using System;
using System.Text;
using System.Web;
using System.Xml;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.Handlers;
using SvnBridge.Interfaces;
using SvnBridge.SourceControl;
using SvnBridge.Utility;

namespace SvnBridge.Nodes
{
    public class FileNode : INode
    {
        private readonly ItemMetaData item;
        private readonly TFSSourceControlProvider sourceControlProvider;

        public FileNode(ItemMetaData item, TFSSourceControlProvider sourceControlProvider)
        {
            this.item = item;
            this.sourceControlProvider = sourceControlProvider;
        }

        #region INode Members

        public string Href(RequestHandlerBase handler)
        {
            string href = item.Name;

            if (!href.StartsWith("/"))
            {
                href = "/" + href;
            }

            if (item.ItemType == ItemType.Folder && !href.EndsWith("/"))
            {
                href += "/";
            }

            return handler.GetLocalPath(Helper.Encode(href));
        }

        public string GetProperty(RequestHandlerBase handler, XmlElement property)
        {
            switch (property.LocalName)
            {
                case "version-controlled-configuration":
                    return GetVersionControlledConfiguration(handler);
                case "resourcetype":
                    return GetResourceType();
                case "baseline-relative-path":
                    return GetBaselineRelativePath();
                case "repository-uuid":
                    return GetRepositoryUUID();
                case "checked-in":
                    return GetCheckedIn(handler);
                case "deadprop-count":
                    return GetDeadPropCount();
                case "creator-displayname":
                    return GetCreatorDisplayName();
                case "creationdate":
                    return GetCreationDate();
                case "version-name":
                    return GetVersionName();
                case "getcontentlength":
                    return GetContentLength();
                case "lockdiscovery":
                    return GetLockDiscovery();
                case "md5-checksum":
                    return GetMd5Checksum();
                default:
                    throw new Exception("Property not found: " + property.LocalName);
            }
        }

        #endregion

        private string GetContentLength()
        {
            return "<lp1:getcontentlength>" + sourceControlProvider.ReadFile(item).Length + "</lp1:getcontentlength>";
        }

        private string GetDeadPropCount()
        {
            return "<lp2:deadprop-count>" + item.Properties.Count + "</lp2:deadprop-count>";
        }

        private static string GetVersionControlledConfiguration(RequestHandlerBase handler)
        {
            return
                "<lp1:version-controlled-configuration><D:href>" + handler.VccPath +
                "</D:href></lp1:version-controlled-configuration>";
        }

        private string GetResourceType()
        {
            if (item.ItemType == ItemType.Folder)
            {
                return "<lp1:resourcetype><D:collection/></lp1:resourcetype>";
            }
            else
            {
                return "<lp1:resourcetype/>";
            }
        }

        private string GetBaselineRelativePath()
        {
            string brl = item.Name;
            if ((brl.Length > 0) && (brl[0] == '/'))
            {
                brl = brl.Substring(1);
            }
            if ((brl.Length > 0) && (brl[brl.Length - 1] == '/'))
            {
                brl = brl.Substring(0, brl.Length - 1);
            }

            brl = Helper.EncodeB(brl);
            if (brl.Length > 0)
            {
                return "<lp2:baseline-relative-path>" + brl + "</lp2:baseline-relative-path>";
            }
            else
            {
                return "<lp2:baseline-relative-path/>";
            }
        }

        private string GetRepositoryUUID()
        {
            return "<lp2:repository-uuid>" + sourceControlProvider.GetRepositoryUuid() + "</lp2:repository-uuid>";
        }

        private string GetCheckedIn(RequestHandlerBase handler)
        {
            string href = handler.GetLocalPath("/!svn/ver/" + item.Revision + "/" + Helper.Encode(item.Name, true));
            return
                "<lp1:checked-in><D:href>" + Helper.UrlEncodeIfNecessary(href) +
                "</D:href></lp1:checked-in>";
        }

        private string GetCreatorDisplayName()
        {
            return "<lp1:creator-displayname>" + item.Author + "</lp1:creator-displayname>";
        }

        private string GetCreationDate()
        {
            return
                "<lp1:creationdate>" + Helper.FormatDate(item.LastModifiedDate.ToUniversalTime()) +
                "</lp1:creationdate>";
        }

        private string GetVersionName()
        {
            return "<lp1:version-name>" + item.Revision + "</lp1:version-name>";
        }

        private static string GetLockDiscovery()
        {
            return "<D:lockdiscovery/>";
        }

        private string GetMd5Checksum()
        {
            return
                "<lp2:md5-checksum>" + Helper.GetMd5Checksum(sourceControlProvider.ReadFile(item)) +
                "</lp2:md5-checksum>";
        }
    }
}
