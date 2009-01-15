using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using CodePlex.TfsLibrary;
using SvnBridge.SourceControl;

namespace UnitTests
{
    public class TFSSourceControlServiceTest
    {
        [Fact]
        public void TestNetworkAccessDeniedExceptionIsReturnedWhenAccessDeniedToServer()
        {
            IRepositoryWebSvcFactory webSvcFactory = new StubRepositoryWebSvcFactory();
            TFSSourceControlService service = new TFSSourceControlService(null, webSvcFactory, null, null, null);
            bool passed = false;

            try
            {
				service.QueryItemsExtended(null, null, null, null, DeletedState.Any, ItemType.Folder);
            }
            catch (NetworkAccessDeniedException)
            {
                passed = true;
            }

            Assert.True(passed);
        }

        public class StubRepositoryWebSvcFactory : IRepositoryWebSvcFactory
        {
            public IRepositoryWebSvc Create(string tfsUrl, System.Net.ICredentials credentials)
            {
                throw new NetworkAccessDeniedException();
            }
        }
}
}
