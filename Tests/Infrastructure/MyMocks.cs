using System;
using System.Net;
using System.Threading;
using Attach;
using CodePlex.TfsLibrary.ObjectModel;
using SvnBridge.Cache;
using SvnBridge.Protocol;
using SvnBridge.SourceControl;
using SvnBridge.Infrastructure;
using SvnBridge.Utility;
using SvnBridge.Interfaces;

namespace Tests
{
    public class MockFramework : AttachFramework
    {
        public Results Attach(Delegate method, Exception exception)
        {
            return base.Attach(method, Return.Exception(exception));
        }

        public Results Attach(Delegate method, object returnValue)
        {
            if (returnValue is Return)
            {
                return base.Attach(method, (Return)returnValue);
            }
            else
            {
                return base.Attach(method, Return.Value(returnValue));
            }
        }

        public Results Attach(Delegate method)
        {
            return base.Attach(method, Return.Nothing);
        }
    }

    public class MyMocks : MockFramework
    {
        public TFSSourceControlProvider CreateTFSSourceControlProviderStub()
        {
            TFSSourceControlProvider stub = CreateObject<TFSSourceControlProvider>("http://www.codeplex.com", null, null, null, null, null, null, null);
            this.Attach((GetRepositoryUuid)stub.GetRepositoryUuid, Return.Value(new Guid("81a5aebe-f34e-eb42-b435-ac1ecbb335f7")));
            this.Attach(stub.GetItemsWithoutProperties, Return.DelegateResult(
                delegate(object[] parameters) { return stub.GetItems((int)parameters[0], (string)parameters[1], (Recursion)parameters[2]); }
            ));
            return stub;
        }

        public delegate void Associate(int workItemId, int changeSetId);
        public delegate void SetWorkItemFixed(int workItemId);
        public delegate void CopyItem(string activityId, string path, string targetPath);
        public delegate bool DeleteItem(string activityId, string path);
        public delegate FolderMetaData GetChangedItems(string path, int versionFrom, int versionTo, UpdateReportData reportData);
        public delegate ItemMetaData GetItemInActivity(string activityId, string path);
        public delegate ItemMetaData GetItems(int version, string path, Recursion recursion);
        public delegate int GetLatestVersion();
        public delegate LogItem GetLog(string path, int versionFrom, int versionTo, Recursion recursion, int maxCount);
        public delegate bool IsDirectory(int version, string path);
        public delegate bool ItemExists(string path, int version);
        public delegate void MakeActivity(string activityId);
        public delegate void MakeCollection(string activityId, string path);
        public delegate void SetActivityComment(string activityId, string comment);
        public delegate MergeActivityResponse MergeActivity(string activityId);
        public delegate byte[] ReadFile(ItemMetaData item);
        public delegate void ReadFileAsync(ItemMetaData item);
        public delegate void SetCredentials(NetworkCredential credentials);
        public delegate void SetProperty(string activityId, string path, string property, string value);
        public delegate int StreamRead(byte[] buffer, int offset, int count);
        public delegate bool WriteFile(string activityId, string path, byte[] fileData);
        public delegate Guid GetRepositoryUuid();
        public delegate bool IsValidTfsServerUrl(string url);
        public delegate IMetaDataRepository Create(ICredentials credentials, string serverUrl, string rootPath);
        public delegate int GetVersionForDate(DateTime date);
        public delegate ProjectLocationInformation GetProjectLocation(string projectName);
        public delegate void Cancel();
        public delegate void DeleteActivity(string activityId);

        public Results Attach(DeleteItem method, bool returnValue)
        {
            return base.Attach((Delegate)method, (object)returnValue);
        }

        public Results Attach(ItemExists method, bool returnValue)
        {
            return base.Attach((Delegate)method, (object)returnValue);
        }

        public Results Attach(ItemExists method, Exception throwException)
        {
            return base.Attach((Delegate)method, throwException);
        }

        public Results Attach(ItemExists method, Return action)
        {
            return base.Attach(method, action);
        }

        public Results Attach(GetLatestVersion method, int returnValue)
        {
            return base.Attach((Delegate)method, (object)returnValue);
        }

        public Results Attach(IsDirectory method, bool returnValue)
        {
            return base.Attach((Delegate)method, (object)returnValue);
        }

        public Results Attach(IsDirectory method, Return action)
        {
            return base.Attach(method, action);
        }

        public Results Attach(MakeActivity method)
        {
            return base.Attach((Delegate)method);
        }

        public Results Attach(MakeActivity method, Exception throwException)
        {
            return base.Attach((Delegate)method, throwException);
        }

        public Results Attach(MakeCollection method)
        {
            return base.Attach((Delegate)method);
        }

        public Results Attach(MakeCollection method, Exception throwException)
        {
            return base.Attach((Delegate)method, throwException);
        }

        public Results Attach(MergeActivity method, MergeActivityResponse returnValue)
        {
            return base.Attach((Delegate)method, (object)returnValue);
        }

        public Results Attach(MergeActivity method, Exception throwException)
        {
            return base.Attach((Delegate)method, throwException);
        }

        public Results Attach(GetItemInActivity method, ItemMetaData returnValue)
        {
            return base.Attach((Delegate)method, (ItemMetaData)returnValue);
        }

        public Results Attach(GetItems method, ItemMetaData returnValue)
        {
            return base.Attach((Delegate)method, (ItemMetaData)returnValue);
        }

        public Results AttachReadFile(ReadFile method, byte[] returnValue)
        {
            return base.Attach((Delegate)method, (byte[])returnValue);
        }

        public Results Attach(ReadFileAsync method, byte[] fileData)
        {
            return base.Attach((Delegate)method, Return.DelegateResult(delegate(object[] parameters)
            {
                ((ItemMetaData)parameters[0]).Base64DiffData = SvnDiffParser.GetSvnDiffData(fileData);
                ((ItemMetaData)parameters[0]).Md5Hash = Helper.GetMd5Checksum(fileData);
                ((ItemMetaData)parameters[0]).DataLoaded = true;
                return null;
            }));
        }

        public Results Attach(SetCredentials method)
        {
            return base.Attach((Delegate)method);
        }

        public Results Attach(WriteFile method, bool returnValue)
        {
            return base.Attach((Delegate)method, (object)returnValue);
        }

        public Results Attach(SetProperty method)
        {
            return base.Attach((Delegate)method);
        }

        public Results Attach(GetChangedItems method, FolderMetaData returnValue)
        {
            return base.Attach((Delegate)method, (object)returnValue);
        }

        public Results Attach(StreamRead method, Exception throwException)
        {
            return base.Attach((Delegate)method, throwException);
        }

        public Results Attach(CopyItem method)
        {
            return base.Attach((Delegate)method);
        }

        public Results Attach(Associate method)
        {
            return base.Attach((Delegate)method);
        }

        public Results Attach(SetWorkItemFixed method)
        {
            return base.Attach((Delegate)method);
        }

        public Results Attach(GetRepositoryUuid method, Return action)
        {
            return base.Attach((Delegate)method, action);
        }

        public Results Attach(GetItems method, Return action)
        {
            return base.Attach((Delegate)method, action);
        }

        public Results Attach(IsValidTfsServerUrl method, Return action)
        {
            return base.Attach((Delegate)method, action);
        }

        public Results Attach(Create method, Return action)
        {
            return base.Attach((Delegate)method, action);
        }

        public Results Attach(GetLog method, Return action)
        {
            return base.Attach((Delegate)method, action);
        }

        public Results Attach(GetVersionForDate method, Return action)
        {
            return base.Attach((Delegate)method, action);
        }

        public Results Attach(GetProjectLocation method, Return action)
        {
            return base.Attach((Delegate)method, action);
        }

        public Results Attach(Cancel method, Return action)
        {
            return base.Attach((Delegate)method, action);
        }

        public Results Attach(ReadFileAsync method, Return action)
        {
            return base.Attach((Delegate)method, action);
        }

        public Results Attach(GetLatestVersion method, Return action)
        {
            return base.Attach((Delegate)method, action);
        }

        public Results Attach(SetActivityComment method, Return action)
        {
            return base.Attach((Delegate)method, action);
        }
    }
}
