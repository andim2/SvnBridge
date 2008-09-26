using System;
using CodePlex.TfsLibrary.ObjectModel;
using SvnBridge.Infrastructure;
using CodePlex.TfsLibrary.RegistrationWebSvc;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using CodePlex.TfsLibrary.Utility;

namespace SvnBridge
{
    public static class BootStrapper
    {
        public static void Start()
        {
            TfsUtil.OnSetupWebRequest = WebRequestSetup.OnWebRequest;
            Container.Register(typeof(IRegistrationService), typeof(RegistrationService));
            Container.Register(typeof(IWebTransferService), typeof(WebTransferService));
            Container.Register(typeof(IRegistrationWebSvcFactory), typeof(RegistrationWebSvcFactory));
            Container.Register(typeof(IRepositoryWebSvcFactory), typeof(RepositoryWebSvcFactory));
            Container.Register(typeof(IFileSystem), typeof(FileSystem));
        }
    }
}