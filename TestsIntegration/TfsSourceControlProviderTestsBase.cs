using System;
using System.IO;
using System.Net;
using System.Text;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RegistrationWebSvc;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using CodePlex.TfsLibrary.Utility;
using SvnBridge;
using SvnBridge.Net;
using IntegrationTests.Properties;
using SvnBridge.Infrastructure;
using SvnBridge.Interfaces;
using SvnBridge.SourceControl;
using Tests;
using SvnBridge.Cache;

namespace IntegrationTests
{
	public abstract class TFSSourceControlProviderTestsBase : IDisposable
	{
        public bool TestRoot = true;
		public string ServerUrl = Settings.Default.ServerUrl;
        protected MyMocks stubs;
		protected const string PROJECT_NAME = "SvnBridgeTesting";
		protected readonly string _activityId;
		protected string testPath;
		protected readonly TFSSourceControlProvider _provider;
		protected int _lastCommitRevision;
		protected readonly TfsWorkItemModifier associateWorkItemWithChangeSet;
		private readonly AuthenticateAsLowPrivilegeUser authenticateAsLowPrivilegeUser;

		public TFSSourceControlProviderTestsBase()
		{
            stubs = new MyMocks();
			RequestCache.Init();
            BootStrapper.Start();
            Container.Resolve<MemoryBasedPersistentCache>().Clear();

			authenticateAsLowPrivilegeUser = new AuthenticateAsLowPrivilegeUser();
			_activityId = Guid.NewGuid().ToString();
			associateWorkItemWithChangeSet = new TfsWorkItemModifier(ServerUrl, GetCredentials());
            _provider = CreateSourceControlProvider(PROJECT_NAME);
            _provider.MakeActivity(_activityId);
            testPath = "/";
        }

        public void Initialize()
        {
            if (!TestRoot)
            {
                testPath = "/Test" + DateTime.Now.ToString("yyyyMMddHHmmss") + "-" + Environment.MachineName + "-" + Guid.NewGuid();
                _provider.MakeCollection(_activityId, testPath);
            }
            Commit();
        }

        public virtual void Dispose()
        {
            Commit();
            if (TestRoot)
            {
                FolderMetaData folder = (FolderMetaData)_provider.GetItems(-1, testPath, Recursion.OneLevel);
                foreach (ItemMetaData item in folder.Items)
                {
                    DeleteItem(testPath + item.Name, false);
                }
                DeleteItem(testPath + Constants.PropFolder, false);
            }
            else
            {
                DeleteItem(testPath, false);
            }
            _provider.MergeActivity(_activityId);
            _provider.DeleteActivity(_activityId);
            authenticateAsLowPrivilegeUser.Dispose();
        }

        public TFSSourceControlProvider CreateSourceControlProvider(string projectName)
		{
			RegistrationWebSvcFactory factory = new RegistrationWebSvcFactory();
			FileSystem system = new FileSystem();
			RegistrationService service = new RegistrationService(factory);
			RepositoryWebSvcFactory factory1 = new RepositoryWebSvcFactory(factory);
			WebTransferService webTransferService = new WebTransferService(system);
			TFSSourceControlService tfsSourceControlService = new TFSSourceControlService(service, factory1, webTransferService, system, stubs.CreateObject<DefaultLogger>());
            ICredentials credentials = GetCredentials();
            FileRepository fileRepository = new FileRepository(ServerUrl, credentials, webTransferService);
            return new TFSSourceControlProvider(
                ServerUrl,
                projectName,
                null,
                tfsSourceControlService,
				associateWorkItemWithChangeSet,
                stubs.CreateObject<DefaultLogger>(),
                stubs.CreateObject<WebCache>(),
                fileRepository);
		}

		protected static ICredentials GetCredentials()
		{
			if (string.IsNullOrEmpty(Settings.Default.Username.Trim()))
			{
				return CredentialCache.DefaultNetworkCredentials;
			}
			return new NetworkCredential(Settings.Default.Username, Settings.Default.Password, Settings.Default.Domain);
		}

		protected void UpdateFile(string path, string fileData, bool commit)
		{
            WriteFile(path, fileData, true);
		}

		protected bool WriteFile(string path, string fileData, bool commit)
		{
			byte[] data = Encoding.Default.GetBytes(fileData);
			return WriteFile(path, data, commit);
		}

		protected bool WriteFile(string path, byte[] fileData, bool commit)
		{
			bool created = _provider.WriteFile(_activityId, path, fileData);
			if (commit)
			{
				Commit();
			}
			return created;
		}

		protected MergeActivityResponse Commit()
		{
			MergeActivityResponse response = _provider.MergeActivity(_activityId);
			_lastCommitRevision = response.Version;
			_provider.DeleteActivity(_activityId);
			_provider.MakeActivity(_activityId);
			RequestCache.Init();
			return response;
		}

		protected void DeleteItem(string path,
								  bool commit)
		{
			_provider.DeleteItem(_activityId, path);
			if (commit)
			{
				Commit();
			}
		}

		protected void CopyItem(string path,
								string newPath,
								bool commit)
		{
			_provider.CopyItem(_activityId, path, newPath);
			if (commit)
			{
				Commit();
			}
		}

		protected void RenameItem(string path,
								  string newPath,
								  bool commit)
		{
			MoveItem(path, newPath, commit);
		}

		protected void MoveItem(string path,
								string newPath,
								bool commit)
		{
			DeleteItem(path, false);
			CopyItem(path, newPath, false);
			if (commit)
			{
				Commit();
			}
		}

		protected int CreateFolder(string path,
								   bool commit)
		{
			_provider.MakeCollection(_activityId, path);
			if (commit)
			{
				return Commit().Version;
			}
			return -1;
		}

		protected string ReadFile(string path)
		{
			ItemMetaData item = _provider.GetItems(-1, path, Recursion.None);
			return GetString(_provider.ReadFile(item));
		}

		protected void SetProperty(string path,
								   string name,
								   string value,
								   bool commit)
		{
			_provider.SetProperty(_activityId, path, name, value);
			if (commit)
			{
				Commit();
			}
		}

		protected string GetString(byte[] data)
		{
			return Encoding.Default.GetString(data);
		}

		protected byte[] GetBytes(string data)
		{
			return Encoding.Default.GetBytes(data);
		}

        protected string MergePaths(string path1, string path2)
        {
            if (path1.EndsWith("/") && path2.StartsWith("/"))
            {
                return path1 + path2.Substring(1);
            }
            else if (!path1.EndsWith("/") && !path2.StartsWith("/"))
            {
                return path1 + "/" + path2;
            }
            else
            {
                return path1 + path2;
            }
        }

		protected bool ResponseContains(MergeActivityResponse response,
										string path,
										ItemType itemType)
		{
			bool found = false;
			foreach (MergeActivityResponseItem item in response.Items)
			{
				if ((item.Path == path) && (item.Type == itemType))
				{
					found = true;
				}
			}

			return found;
		}
	}
}