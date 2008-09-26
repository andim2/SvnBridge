using System;
using System.Net;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using Xunit;
using SvnBridge.Interfaces;

namespace SvnBridge.SourceControl
{
	//public class ProjectInformationRepositoryTest : IDisposable
	//{
	//    #region Setup/Teardown

	//    public ProjectInformationRepositoryTest()
	//    {
	//        mocks = new MockRepository();
	//        sourceControlService = mocks.DynamicMock<ITFSSourceControlService>();
	//        cache = mocks.DynamicMock<ICache>();
	//    }

	//    public void Dispose()
	//    {
	//        mocks.VerifyAll();
	//    }

	//    #endregion

	//    private MockRepository mocks;
	//    private ITFSSourceControlService sourceControlService;
	//    private ICache cache;

	//    [Fact]
	//    public void GetProjectInforation_WillQueryServerForProject()
	//    {
	//        string serverUrl = "http://codeplex-tfs3:8080";
	//        sourceControlService.QueryItems(null,
	//                                        null,
	//                                        null,
	//                                        RecursionType.None,
	//                                        null,
	//                                        DeletedState.Any,
	//                                        ItemType.Any);
	//        SourceItem sourceItem = new SourceItem();
	//        sourceItem.RemoteName = "$/test";
	//        LastCall.Constraints(Is.Equal(serverUrl),
	//                             Is.Anything(),
	//                             Is.Anything(),
	//                             Is.Anything(),
	//                             Is.Anything(),
	//                             Is.Anything(),
	//                             Is.Anything())
	//            .Return(new SourceItem[] {sourceItem,});


	//        IProjectInformationRepository repository =
	//            new ProjectInformationRepository(cache, sourceControlService, serverUrl);
	//        mocks.ReplayAll();

	//        ProjectLocationInformation location =
	//            repository.GetProjectLocation(CredentialCache.DefaultCredentials, "blah");
	//    }

	//    [Fact]
	//    public void GetProjectInforation_WillReturnRemoteProjectName()
	//    {
	//        string serverUrl = "http://codeplex-tfs3:8080";
	//        sourceControlService.QueryItems(null,
	//                                        null,
	//                                        null,
	//                                        RecursionType.None,
	//                                        null,
	//                                        DeletedState.Any,
	//                                        ItemType.Any);
	//        SourceItem sourceItem = new SourceItem();
	//        sourceItem.RemoteName = "$/test";
	//        LastCall.Constraints(Is.Equal(serverUrl),
	//                             Is.Anything(),
	//                             Is.Anything(),
	//                             Is.Anything(),
	//                             Is.Anything(),
	//                             Is.Anything(),
	//                             Is.Anything())
	//            .Return(new SourceItem[] {sourceItem,});


	//        IProjectInformationRepository repository =
	//            new ProjectInformationRepository(cache, sourceControlService, serverUrl);
	//        mocks.ReplayAll();

	//        ProjectLocationInformation location =
	//            repository.GetProjectLocation(CredentialCache.DefaultCredentials, "blah");

	//        Assert.Equal("test", location.RemoteProjectName);
	//    }

	//    [Fact]
	//    public void GetProjectInformation_WillQueryAllServers()
	//    {
	//        string multiServers = "http://codeplex-tfs3:8080,http://codeplex-tfs2:8080,http://codeplex-tfs1:8080";

	//        SourceItem sourceItem = new SourceItem();
	//        sourceItem.RemoteName = "$/test";

	//        sourceControlService.QueryItems(null,
	//                                        null,
	//                                        null,
	//                                        RecursionType.None,
	//                                        null,
	//                                        DeletedState.Any,
	//                                        ItemType.Any);
	//        LastCall.IgnoreArguments().Repeat.Twice().Return(new SourceItem[0]);

	//        sourceControlService.QueryItems(null,
	//                                        null,
	//                                        null,
	//                                        RecursionType.None,
	//                                        null,
	//                                        DeletedState.Any,
	//                                        ItemType.Any);

	//        LastCall.IgnoreArguments().Return(new SourceItem[] {sourceItem});

	//        mocks.ReplayAll();

	//        IProjectInformationRepository repository =
	//            new ProjectInformationRepository(cache, sourceControlService, multiServers);

	//        repository.GetProjectLocation(null, "as");
	//    }

	//    [Fact]
	//    public void IfProjectNotFound_WillThrow()
	//    {
	//        mocks.ReplayAll();

	//        IProjectInformationRepository repository =
	//            new ProjectInformationRepository(cache, sourceControlService, "http://not.used");

	//        Exception result = Record.Exception(delegate { repository.GetProjectLocation(null, "blah"); });

	//        Assert.IsType(typeof(InvalidOperationException), result);
	//        Assert.Equal("Could not find project 'blah' in: http://not.used", result.Message);
	//    }

	//    [Fact]
	//    public void WillGetFromCacheIfFound()
	//    {
	//        Expect.Call(cache.Get("GetProjectLocation-blah")).Return(new CachedResult(new ProjectLocationInformation("blah", "http")));

	//        mocks.ReplayAll();

	//        IProjectInformationRepository repository =
	//            new ProjectInformationRepository(cache, sourceControlService, "http://not.used");

	//        ProjectLocationInformation location = repository.GetProjectLocation(null, "blah");
	//        Assert.NotNull(location);
	//    }

	//    [Fact]
	//    public void WillSetInCacheAfterFindingFromServer()
	//    {
	//        string serverUrl = "http://codeplex-tfs3:8080";
	//        sourceControlService.QueryItems(null,
	//                                        null,
	//                                        null,
	//                                        RecursionType.None,
	//                                        null,
	//                                        DeletedState.Any,
	//                                        ItemType.Any);
	//        SourceItem sourceItem = new SourceItem();
	//        sourceItem.RemoteName = "$/test";
	//        LastCall.Constraints(Is.Equal(serverUrl),
	//                             Is.Anything(),
	//                             Is.Anything(),
	//                             Is.Anything(),
	//                             Is.Anything(),
	//                             Is.Anything(),
	//                             Is.Anything())
	//            .Return(new SourceItem[] {sourceItem,});

	//        cache.Set(null, null);
	//        LastCall.IgnoreArguments();


	//        IProjectInformationRepository repository =
	//            new ProjectInformationRepository(cache, sourceControlService, serverUrl);
	//        mocks.ReplayAll();

	//        ProjectLocationInformation location =
	//            repository.GetProjectLocation(CredentialCache.DefaultCredentials, "blah");

	//        Assert.Equal("test", location.RemoteProjectName);
	//    }
	//}
}