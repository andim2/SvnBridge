using System;
using System.Collections.Generic;
using System.IO;
using System.Threading; // Thread.Sleep()
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.Handlers;
using SvnBridge.Protocol;
using SvnBridge.SourceControl;
using SvnBridge.Utility; // Helper.Encode() etc.

namespace SvnBridge.Infrastructure
{
	internal class UpdateReportService
	{
		private readonly RequestHandlerBase handler;
        private readonly TFSSourceControlProvider sourceControlProvider;

        public UpdateReportService(RequestHandlerBase handler, TFSSourceControlProvider sourceControlProvider)
		{
			this.handler = handler;
			this.sourceControlProvider = sourceControlProvider;
		}

		public void ProcessUpdateReportForFile(UpdateReportData updateReportRequest, ItemMetaData item, StreamWriter output, bool parentFolderWasDeleted)
		{
			if (item is DeleteMetaData)
			{
                if (!parentFolderWasDeleted)
                {
                    output.Write("<S:delete-entry name=\"" + GetEncodedNamePart(item) + "\"/>\n");
                }
			}
			else
			{
				bool existingFile = false;
				string srcPath = GetSrcPath(updateReportRequest);
                int clientRevisionForItem = GetClientRevisionFor(updateReportRequest.Entries, StripBasePath(item, srcPath));
				if (ItemExistsAtTheClient(item, updateReportRequest, srcPath, clientRevisionForItem))
				{
					existingFile = true;
				}

				//another item with the same name already exists, need to remove it.
				if (!parentFolderWasDeleted && ShouldDeleteItemBeforeSendingToClient(item, updateReportRequest, srcPath, clientRevisionForItem, existingFile))
				{
					output.Write("<S:delete-entry name=\"" + GetEncodedNamePart(item) + "\"/>\n");
				}

				if (existingFile)
				{
					output.Write("<S:open-file name=\"" + GetEncodedNamePart(item) + "\" rev=\"" +
                                 clientRevisionForItem + "\">\n");
				}
				else
				{
					output.Write("<S:add-file name=\"" + GetEncodedNamePart(item) + "\">\n");
				}

        UpdateReportWriteItemAttributes(output, item);

				while (item.DataLoaded == false)
					Thread.Sleep(100);
                                var base64DiffData = item.Base64DiffData;
                                // Immediately release data memory from item's reach
                                // (reduce GC memory management pressure)
                                item.DataLoaded = false;
                                item.Base64DiffData = null;

				output.Write("<S:txdelta>");
                // KEEP THIS WRITE ACTION SEPARATE! (avoid huge-string alloc):
                output.Write(base64DiffData);
				output.Write("\n</S:txdelta>");
                output.Write("<S:prop><V:md5-checksum>" + item.Md5Hash + "</V:md5-checksum></S:prop>\n");

                if (existingFile)
				{
					output.Write("</S:open-file>\n");
				}
				else
				{
					output.Write("</S:add-file>\n");
				}
			}
		}

        private static string GetEncodedNamePart(ItemMetaData item)
        {
            return Helper.EncodeB(GetFileName(item.Name));
        }

		private bool ItemExistsAtTheClient(ItemMetaData item, UpdateReportData updateReportRequest, string srcPath, int clientRevisionForItem)
		{
			return updateReportRequest.IsCheckOut == false &&
                   IsMissing(updateReportRequest, srcPath, item.Name) == false &&
			       // we need to check both name and id to ensure that the item was not renamed
			       sourceControlProvider.ItemExists(item.Name, clientRevisionForItem) &&
			       sourceControlProvider.ItemExists(item.Id, clientRevisionForItem);
		}

		private string GetSrcPath(UpdateReportData updateReportRequest)
		{
			string url = handler.GetLocalPathFromUrl(updateReportRequest.SrcPath);
			if (updateReportRequest.UpdateTarget != null)
				return url + "/" + updateReportRequest.UpdateTarget;
			return url;
		}

		public void ProcessUpdateReportForDirectory(UpdateReportData updateReportRequest, FolderMetaData folder, StreamWriter output, bool rootFolder, bool parentFolderWasDeleted)
		{
			if (folder is DeleteFolderMetaData)
			{
                if (!parentFolderWasDeleted)
                {
                    output.Write("<S:delete-entry name=\"" + GetEncodedNamePart(folder) + "\"/>\n");
                }
			}
			else
			{
				bool existingFolder = false;
                bool folderWasDeleted = parentFolderWasDeleted;
                if (rootFolder)
				{
					output.Write("<S:open-directory rev=\"" + updateReportRequest.Entries[0].Rev + "\">\n");
				}
				else
				{
					string srcPath = GetSrcPath(updateReportRequest);
                    int clientRevisionForItem = GetClientRevisionFor(updateReportRequest.Entries, StripBasePath(folder, srcPath));
					if (ItemExistsAtTheClient(folder, updateReportRequest, srcPath, clientRevisionForItem))
					{
						existingFolder = true;
					}

					//another item with the same name already exists, need to remove it.
					if (!parentFolderWasDeleted && ShouldDeleteItemBeforeSendingToClient(folder, updateReportRequest, srcPath, clientRevisionForItem, existingFolder))
					{
						output.Write("<S:delete-entry name=\"" + GetEncodedNamePart(folder) + "\"/>\n");
                        folderWasDeleted = true;
					}

					if (existingFolder)
					{
						output.Write("<S:open-directory name=\"" + GetEncodedNamePart(folder) +
									 "\" rev=\"" + updateReportRequest.Entries[0].Rev + "\">\n");
					}
					else
					{
						output.Write("<S:add-directory name=\"" + GetEncodedNamePart(folder) +
									 "\" bc-url=\"" + handler.GetLocalPath("/!svn/bc/" + folder.Revision + "/" + Helper.Encode(folder.Name, true)) +
									 "\">\n");
					}
				}
				if (!rootFolder || updateReportRequest.UpdateTarget == null)
				{
          UpdateReportWriteItemAttributes(output, folder);
				}

				for (int i = 0; i < folder.Items.Count; i++)
				{
					ItemMetaData item = folder.Items[i];
					if (item.ItemType == ItemType.Folder)
					{
						ProcessUpdateReportForDirectory(updateReportRequest, (FolderMetaData)item, output, false, folderWasDeleted);
					}
					else
					{
						ProcessUpdateReportForFile(updateReportRequest, item, output, folderWasDeleted);
					}
				}
				output.Write("<S:prop></S:prop>\n");
				if (rootFolder || existingFolder)
				{
					output.Write("</S:open-directory>\n");
				}
				else
				{
					output.Write("</S:add-directory>\n");
				}
			}
		}

		private bool ShouldDeleteItemBeforeSendingToClient(ItemMetaData folder,
			UpdateReportData updateReportRequest,
			string srcPath,
			int clientRevisionForItem,
			bool existingFolder)
		{
			return existingFolder == false && updateReportRequest.IsCheckOut == false &&
                   IsMissing(updateReportRequest, srcPath, folder.Name) == false &&
				   sourceControlProvider.ItemExists(folder.Name, clientRevisionForItem);
		}

        private void UpdateReportWriteItemAttributes(TextWriter output, ItemMetaData item)
        {
            string svnVerLocalPath = handler.GetLocalPath("/!svn/ver/" + item.Revision + "/" + Helper.Encode(item.Name, true));
            output.Write("<D:checked-in><D:href>" + svnVerLocalPath + "</D:href></D:checked-in>\n");
            output.Write("<S:set-prop name=\"svn:entry:committed-rev\">" + item.Revision + "</S:set-prop>\n");
            output.Write("<S:set-prop name=\"svn:entry:committed-date\">" + Helper.FormatDate(item.LastModifiedDate) + "</S:set-prop>\n");
            output.Write("<S:set-prop name=\"svn:entry:last-author\">" + item.Author + "</S:set-prop>\n");
            output.Write("<S:set-prop name=\"svn:entry:uuid\">" + sourceControlProvider.GetRepositoryUuid() + "</S:set-prop>\n");
            foreach (KeyValuePair<string, string> property in item.Properties)
            {
                output.Write("<S:set-prop name=\"" + property.Key + "\">" + property.Value + "</S:set-prop>\n");
            }
        }

		private static string GetFileName(string path)
		{
			int slashIndex = path.LastIndexOfAny(new char[] { '/', '\\' });
			return path.Substring(slashIndex + 1);
		}

        private string StripBasePath(ItemMetaData item, string basePath)
        {
            string name = item.Name;

            if (name.StartsWith("/"))
                name = name.Substring(1);

            if (basePath.StartsWith("/"))
                basePath = basePath.Substring(1);

            basePath = basePath + "/";

            if (name.StartsWith(basePath))
            {
                name = name.Substring(basePath.Length);
                if (name.StartsWith(@"/"))
                    name = name.Substring(1);
            }
            return name;
        }

        private int GetClientRevisionFor(List<EntryData> entries, string name)
        {
            EntryData bestMatch = entries[0];

            foreach (EntryData entry in entries)
            {
                if (entry.path == name)// found a best match
                {
                    bestMatch = entry;
                    break;
                }

                if (entry.path == null || name.StartsWith(entry.path, StringComparison.InvariantCultureIgnoreCase) == false)
                    continue;

                // if the current entry is longer than the previous best match, then this
                // is a better match, because it is more deeply nested, so likely
                // to be a better parent
                if (bestMatch.path == null || bestMatch.path.Length < entry.path.Length)
                    bestMatch = entry;
            }
            return int.Parse(bestMatch.Rev);
        }

        private bool IsMissing(UpdateReportData data, string localPath, string name)
        {
            if (data.Missing == null || data.Missing.Count == 0)
                return false;

            string path = localPath.Substring(1);
            if (path.EndsWith("/") == false)
                path += "/";
            if (name.StartsWith(path))
                name = name.Substring(path.Length);

            if (data.Missing.Contains(name))
                return true;
            foreach (string missing in data.Missing)
            {
                if (name.StartsWith(missing))// the missing is the parent of this item
                    return true;
            }
            return false;
        }
    }
}
