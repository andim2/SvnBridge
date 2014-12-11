using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using CodePlex.TfsLibrary.ClientEngine;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using CodePlex.TfsLibrary.Utility;

namespace CodePlex.TfsLibrary
{
    public class PatchEngine : IPatchEngine
    {
        readonly IFileSystem fileSystem;
        readonly ITfsEngine tfsEngine;

        public PatchEngine(ITfsEngine tfsEngine,
                           IFileSystem fileSystem)
        {
            this.tfsEngine = tfsEngine;
            this.fileSystem = fileSystem;
        }

        public void ApplyPatch(string directory,
                               string patchXml,
                               SourceItemCallback callback)
        {
            TfsFolderInfo folderInfo = tfsEngine.GetFolderInfo(directory);
            XmlSerializer serializer = new XmlSerializer(typeof(PatchElement));
            PatchElement patch;

            try
            {
                using (StringReader reader = new StringReader(patchXml))
                    patch = (PatchElement)serializer.Deserialize(reader);
            }
            catch (InvalidOperationException ex)
            {
                throw new IOException("The patch XML appears to be malformed.", ex);
            }

            patch.add = patch.add ?? new AddElement[0];
            patch.delete = patch.delete ?? new DeleteElement[0];
            patch.update = patch.update ?? new UpdateElement[0];

            if (!ApplyPatch_ValidatePatchInsideLocalDirectory(folderInfo.ServerPath, patch))
            {
                callback(SourceItem.FromLocalPath(directory), SourceItemResult.E_PatchFilesOutsideLocalDirectory);
                return;
            }

            bool foundErrors = false;

            tfsEngine.Status(directory, VersionSpec.Latest, true, false,
                             delegate(SourceItem item,
                                      SourceItemResult result)
                             {
                                 item.RemoteName = TfsUtil.LocalPathToServerPath(folderInfo.ServerPath,
                                                                                 directory,
                                                                                 item.LocalName,
                                                                                 item.ItemType);

                                 switch (item.LocalItemStatus)
                                 {
                                     case SourceItemStatus.Add:
                                     case SourceItemStatus.Modified:
                                     case SourceItemStatus.Delete:
                                         if (ApplyPatch_FindElement(item.RemoteName, patch))
                                         {
                                             foundErrors = true;
                                             callback(item, SourceItemResult.E_WontClobberLocalItem);
                                         }
                                         break;
                                 }
                             });

            if (foundErrors)
                return;

            foreach (DeleteElement delete in patch.delete)
                ApplyUpdate_Delete(delete, folderInfo.ServerPath, directory, callback);

            foreach (UpdateElement update in patch.update)
                ApplyPatch_Update(update, folderInfo.ServerPath, directory, callback);

            foreach (AddElement add in patch.add)
                ApplyPatch_Add(add, folderInfo.ServerPath, directory, callback);
        }

        void ApplyPatch_Add(AddElement add,
                            string serverPath,
                            string directory,
                            SourceItemCallback callback)
        {
            string localPath = TfsUtil.ServerPathToLocalPath(serverPath, directory, add.path);

            if (add.type == ItemTypeElement.folder)
                fileSystem.EnsurePath(localPath);
            else
            {
                if (fileSystem.FileExists(localPath))
                {
                    callback(SourceItem.FromLocalPath(localPath), SourceItemResult.E_WontClobberLocalItem);
                    return;
                }

                fileSystem.WriteAllBytes(localPath, CompressionUtil.Decompress(add.Value, AddElement.ToCompressionType(add.compression)));
            }

            tfsEngine.Add(localPath, false, callback);
        }

        static bool ApplyPatch_FindElement(string serverPath,
                                           PatchElement patch)
        {
            foreach (AddElement elem in patch.add)
                if (string.Compare(elem.path, serverPath, true) == 0)
                    return true;

            foreach (DeleteElement elem in patch.delete)
                if (string.Compare(elem.path, serverPath, true) == 0)
                    return true;

            foreach (UpdateElement elem in patch.update)
                if (string.Compare(elem.path, serverPath, true) == 0)
                    return true;

            return false;
        }

        void ApplyPatch_Update(UpdateElement update,
                               string serverPath,
                               string directory,
                               SourceItemCallback callback)
        {
            string localPath = TfsUtil.ServerPathToLocalPath(serverPath, directory, update.path);

            tfsEngine.Update(localPath, false, VersionSpec.FromChangeset(update.csid), null);
            fileSystem.WriteAllBytes(localPath, CompressionUtil.Decompress(update.Value, AddElement.ToCompressionType(update.compression)));

            callback(SourceItem.FromLocalItem(0, ItemType.File, SourceItemStatus.Unmodified, SourceItemStatus.Unmodified,
                                              localPath, null, update.csid, Constants.NullChangesetId, null), SourceItemResult.S_Ok);
        }

        static bool ApplyPatch_ValidatePatchInsideLocalDirectory(string serverPath,
                                                                 PatchElement patch)
        {
            foreach (AddElement elem in patch.add)
                if (!elem.path.StartsWith(serverPath))
                    return false;

            foreach (DeleteElement elem in patch.delete)
                if (!elem.path.StartsWith(serverPath))
                    return false;

            foreach (UpdateElement elem in patch.update)
                if (!elem.path.StartsWith(serverPath))
                    return false;

            return true;
        }

        void ApplyUpdate_Delete(DeleteElement delete,
                                string serverPath,
                                string directory,
                                SourceItemCallback callback)
        {
            string localPath = TfsUtil.ServerPathToLocalPath(serverPath, directory, delete.path);
            SourceItem item = tfsEngine.GetSourceItem(localPath);

            if (item.LocalItemStatus == SourceItemStatus.Modified || item.LocalItemStatus == SourceItemStatus.Unversioned)
                callback(item, SourceItemResult.E_WontClobberLocalItem);
            else
                tfsEngine.Delete(localPath, false, callback);
        }

        public string CreatePatch(string directory,
                                  SourceItemCallback callback)
        {
            List<DeleteElement> deleteElements = new List<DeleteElement>();
            List<UpdateElement> updateElements = new List<UpdateElement>();
            List<AddElement> addElements = new List<AddElement>();
            TfsFolderInfo folderInfo = tfsEngine.GetFolderInfo(directory);

            if (!fileSystem.DirectoryExists(directory))
            {
                callback(SourceItem.FromLocalPath(directory), SourceItemResult.E_DirectoryNotFound);
                return null;
            }

            if (!tfsEngine.IsFolderTracked(directory))
            {
                callback(SourceItem.FromLocalPath(directory), SourceItemResult.E_NotUnderSourceControl);
                return null;
            }

            tfsEngine.Status(directory, VersionSpec.Latest, true, false,
                             delegate(SourceItem item,
                                      SourceItemResult result)
                             {
                                 switch (item.LocalItemStatus)
                                 {
                                     case SourceItemStatus.Add:
                                         callback(item, result);
                                         addElements.Add(AddElement.FromSourceItem(item,
                                                                                   folderInfo.ServerPath,
                                                                                   directory,
                                                                                   fileSystem));
                                         break;

                                     case SourceItemStatus.Delete:
                                         callback(item, result);
                                         deleteElements.Add(DeleteElement.FromSourceItem(item,
                                                                                         folderInfo.ServerPath,
                                                                                         directory));
                                         break;

                                     case SourceItemStatus.Modified:
                                         callback(item, result);
                                         updateElements.Add(UpdateElement.FromSourceItem(item,
                                                                                         folderInfo.ServerPath,
                                                                                         directory,
                                                                                         fileSystem));
                                         break;

                                     case SourceItemStatus.Missing:
                                     case SourceItemStatus.Unversioned:
                                         callback(item, result);
                                         break;
                                 }
                             });

            if (deleteElements.Count == 0 && addElements.Count == 0 && updateElements.Count == 0)
                return null;

            PatchElement patchElement = new PatchElement();
            patchElement.delete = deleteElements.ToArray();
            patchElement.add = addElements.ToArray();
            patchElement.update = updateElements.ToArray();

            using (MemoryStream memoryStream = new MemoryStream())
            {
                XmlSerializer serializer = new XmlSerializer(typeof(PatchElement));
                serializer.Serialize(memoryStream, patchElement);

                memoryStream.Position = 0;

                using (StreamReader reader = new StreamReader(memoryStream))
                    return reader.ReadToEnd();
            }
        }
    }
}