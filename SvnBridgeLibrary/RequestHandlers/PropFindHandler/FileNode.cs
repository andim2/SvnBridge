using CodePlex.TfsLibrary.RepositoryWebSvc; // ItemType
using SvnBridge.Handlers; // RequestHandlerBase
using SvnBridge.SourceControl; // ItemMetaData, TFSSourceControlProvider
using SvnBridge.SourceControl.Dto; // ItemProperties
using SvnBridge.Utility; // Helper.Encode()

namespace SvnBridge.Nodes
{
    public class FileNode : NodeBase
    {
        private readonly ItemMetaData item;
        private readonly TFSSourceControlProvider sourceControlProvider;

        public FileNode(ItemMetaData item, TFSSourceControlProvider sourceControlProvider)
        {
            this.item = item;
            this.sourceControlProvider = sourceControlProvider;
        }

        #region INode Members

        public override string Href(RequestHandlerBase handler)
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

        protected override string GetProperty_Core(RequestHandlerBase handler, string propertyName)
        {
            switch (propertyName)
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
                case "checked-out":
                    return GetCheckedOut(handler);
                case "deadprop-count":
                    return GetDeadPropCount();
                case "creator-displayname":
                    return GetCreatorDisplayName();
                case "displayname": // e.g. Konqueror webdav://
                    return GetDisplayName();
                case "creationdate":
                    return GetCreationDate();
                case "getetag": // Queried by e.g. davfs2.
                    return GetETag();
                case "getlastmodified": // queried by WebDAV clients at least (Cadaver)
                    return GetLastModified();
                case "executable": // queried by WebDAV clients at least (Cadaver)
                    return GetExecutable();
                case "version-name":
                    return GetVersionName();
                case "getcontentlength":
                    return GetContentLength();
                case "lockdiscovery":
                    return GetLockDiscovery();
                case "md5-checksum":
                    return GetMd5Checksum();
                case "quota-available-bytes": // Queried by e.g. davfs2.
                    return GetQuotaAvailableBytes();
                default:
                    return null;
            }
        }

        #endregion

        private string GetContentLength()
        {
            // Ouch, this is obviously very expensive (a dominating performance hotspot of properties),
            // but there probably isn't much we can do
            // (provider APIs don't seem to provide an interface for
            // querying further details about an item - this also applies to GUIs on Windows).
            // Nope, that is not really true: there's both SourceItem.RemoteSize and Item.len members.
            // But these two are a CodePlex.TfsLibrary dependency and We Don't Do That Here (well, "try to") -
            // so a compromise would be adding a .length member to ItemMetaData,
            // however an extra member there is quite painful
            // as long as we are only making use of it at this single place here.
            int content_length = sourceControlProvider.ReadFile(item).Length;
            return "<lp1:getcontentlength>" + content_length + "</lp1:getcontentlength>";
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
            string href = handler.GetLocalPath(SVNGeneratorHelpers.GetSvnVerFromRevisionLocation(item.Revision, item.Name, true));
            return
                "<lp1:checked-in><D:href>" + Helper.UrlEncodeIfNecessary(href) +
                "</D:href></lp1:checked-in>";
        }

        private static string GetCheckedOut(RequestHandlerBase handler)
        {
            // STUB!! (for WebDAV - Cadaver)
            return "<D:checked-out/>";
        }

        private string GetCreatorDisplayName()
        {
            return "<lp1:creator-displayname>" + item.Author + "</lp1:creator-displayname>";
        }

        private static string GetDisplayName()
        {
            // See "DAV:displayname handling" https://issues.apache.org/bugzilla/show_bug.cgi?id=24735
            // (rationale: don't return property - like Apache mod_dav - since we cannot indicate
            // specifics anyway)
            return "<D:displayname/>"; // hmm - is this "empty" or "no" property?
        }

        private string GetETag()
        {
            return WebDAVGeneratorHelpers.GetETag_revision_item("lp1", item.Revision, item.Name);
        }

        private string GetCreationDate()
        {
            // Ouch, it's a problem that we only have a LastModifiedDate member whereas
            // we're supposed to return initial creation date - right?
            // Well, it depends on what exactly creationdate would be about -
            // and no matter what it is
            // we should be able to determine the *original*
            // creation date via an SCM helper method
            // which traces things back to the proper item
            // at the revision which would be the initial revision
            // that is relevant for this element.
            // Indeed something like this, see:
            // "Re: svn commit: rev 2637 - trunk/subversion/mod_dav_svn"
            //   http://svn.haxx.se/dev/archive-2002-07/1363.shtml
            return
                "<lp1:creationdate>" + Helper.FormatDate(item.LastModifiedDate) +
                "</lp1:creationdate>";
        }

        private string GetLastModified()
        {
            return
                "<lp1:getlastmodified>" + Helper.FormatDate(item.LastModifiedDate) +
                "</lp1:getlastmodified>";
        }

        private string GetExecutable()
        {
            bool isExecutable = GetStatus_svn_executable(item);
            // See "mod_dav's custom properties" http://www.webdav.org/mod_dav/
            // Returns T or F (case is significant) to indicate true/false bool of executable status of a resource (file).
            return
                "<lp2:executable>" + GetBool_TF(isExecutable) +
                "</lp2:executable>";
        }

        private bool GetStatus_svn_executable(ItemMetaData item)
        {
            bool isExecutable = false;
            // "This property is not defined on collections"
            bool isCollection = (item.ItemType == ItemType.Folder);
            if (!isCollection)
            {
                ItemProperties properties = sourceControlProvider.ReadPropertiesForItem(item);
                isExecutable = GetProperty_svn_executable(properties);
            }
            return isExecutable;
        }

        private static bool GetProperty_svn_executable(ItemProperties properties)
        {
            bool isExecutable_default = false;
            bool isExecutable = isExecutable_default;
            if (null != properties)
            {
                foreach (var property in properties.Properties)
                {
                    if (property.Name.Equals("svn:executable"))
                    {
                        isExecutable = (property.Value.Equals("*"));
                        // For now, prefer having no "break;"
                        // (perhaps there might be multiple such property strings?
                    }
                }
            }
            return isExecutable;
        }

        private static string GetBool_TF(bool true_false)
        {
            return true_false ? "T" : "F";
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

        private static string GetQuotaAvailableBytes()
        {
            // STUB (I don't think we should/can indicate any quota support in this server).
            return "<D:quota-available-bytes/>";
        }
    }
}
