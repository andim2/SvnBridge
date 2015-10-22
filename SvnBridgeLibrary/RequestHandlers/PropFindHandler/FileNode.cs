using System; // String.Format()
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
                case "getcontentlanguage": // e.g. Konqueror 4.8.5 webdav://
                    return GetContentLanguage();
                case "getcontentlength":
                    return GetContentLength();
                case "getcontenttype": // e.g. Konqueror 4.8.5 webdav://
                    return GetContentType();
                case "lockdiscovery":
                    return GetLockDiscovery();
                case "md5-checksum":
                    return GetMd5Checksum();
                case "quota-available-bytes": // Queried by e.g. davfs2.
                    return GetQuotaAvailableBytes();
                case "source": // e.g. Konqueror 4.8.5 webdav://
                    return GetSource(handler);
                case "supportedlock": // e.g. Konqueror 4.8.5 webdav://
                    return GetSupportedLock();
                default:
                    return null;
            }
        }

        #endregion

        /// <remarks>
        /// RFC2068
        ///   "14.13 Content-Language":
        /// "
        /// If no Content-Language is specified, the default is that the content
        /// is intended for all language audiences. This may mean that the sender
        /// does not consider it to be specific to any natural language, or that
        /// the sender does not know for which language it is intended.
        /// "
        /// I believe that items in TFS do carry some encoding field somewhere
        /// (was that in history queries?)
        /// which could be used to clumsily infer a "language" from.
        /// However, as long as we don't have that available
        /// simply return "unknown" language.
        /// </remarks>
        private string GetContentLanguage()
        {
            string contentLanguageUnknown = "";
            return contentLanguageUnknown;
        }

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

        /// <remarks>
        /// TODO: Hmm, what to do here?
        /// TFS possibly does not directly offer functionality
        /// to indicate content type,
        /// so perhaps we would have to resort
        /// to manually and painfully passing our huge-blob content
        /// to some system-side content type (MIME type!!) detection API.
        /// So, the best we can do for now
        /// is indicate unknown MIME type (empty string),
        /// since subsequent layers likely are currently better prepared
        /// to make some guesses.
        /// And quite possibly we shouldn't directly indicate "unknown" type
        /// ("application/octet-stream") either,
        /// since that would possibly forego any type guessing
        /// that might happen subsequently.
        /// So, really do return an empty string only.
        /// And yes indeed, now that we do support this property
        /// with "unsupported" hint (empty string),
        /// Konqueror does seem to do MIME type heuristics
        /// since it does switch its view
        /// from bare file entries listing
        /// to properly item type distinct icon view.
        /// </remarks>
        private string GetContentType()
        {
            string unknownContentType = "";
            string contentType;

            bool isFolder = (ItemType.Folder == item.ItemType);
            bool haveContentType = !(isFolder);
            if (haveContentType)
            {
                contentType = unknownContentType;
            }
            else
            {
                contentType = "";
            }

            return contentType;
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

        /// <remarks>
        ///  RFC2518:
        ///    "13.10.  source Property"
        ///    "5.4 Source Resources and Output Resources"
        /// </remarks>
        private string GetSource(RequestHandlerBase handler)
        {
            //string test1 = handler.GetLocalPathFromUrl(item.Name);
            //string test2 = handler.GetLocalPath(item.Name);
            string href = Href(handler);
            // In our case I'd think
            // that the output (processed) location
            // does not differ from the source (raw item) location,
            // thus it ought to be identical.
            string src = href;
            string dst = href;
            // Hmm, that cannot be done easily
            // since D:source likely is expected to return
            // fully host-based URIs,
            // however host parts are only available in context.Request.Uri areas
            // which are not (supposed to be) available
            // in FileNode-restricted sub scopes
            // (after all the host part is emphatically *NOT*
            // a part of the "inner" properties of a file node!).
            // One could possibly even argue
            // that we have a layer violation
            // even within the spec
            // in that it suggests returning host-related full paths
            // at the property implementation of a specific node.
            // The only meaningful solution would probably be
            // to specially handle "source" property
            // at the PROPFIND layer,
            // by passing the INode into a helper method
            // which produces host-augmented src, dst pairs for an INode,
            // to then be generated into XML format.
            // Oh well, so perhaps simply return an empty <D:source> elem.
            return "<D:source/>";
            //return GenerateSourceProperty(src, dst);
        }

        /// <remarks>
        /// TODO: we should be supporting resource locking
        /// (should be doable via usual TFS checkout for edit stuff etc.).
        /// As long as we don't support it,
        /// do indicate so.
        /// </remarks>
        private static string GetSupportedLock()
        {
            string noLockSupported = "";
            return noLockSupported;
        }

        private static string GenerateSourceProperty(string src, string dst)
        {
            return String.Format(
                "<D:source>" +
                "<D:link>" +
                "<D:src>{0}</D:src>" +
                "<D:dst>{1}</D:dst>" +
                "</D:link>" +
                "</D:source>",
                src,
                dst);
        }
    }
}
