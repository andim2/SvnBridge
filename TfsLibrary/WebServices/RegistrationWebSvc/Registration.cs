using System;
using System.Net;
using CodePlex.TfsLibrary.ObjectModel;

namespace CodePlex.TfsLibrary.RegistrationWebSvc
{
    public partial class Registration : IRegistrationWebSvc
    {
        public Registration(string tfsUrl,
                            ICredentials credentials)
        {
            if (!tfsUrl.EndsWith("/"))
                tfsUrl += "/";

            Url = tfsUrl + "Services/v1.0/Registration.asmx";
            Credentials = credentials;
        }

        protected override WebRequest GetWebRequest(Uri uri)
        {
            return TfsUtil.SetupWebRequest(base.GetWebRequest(uri), Credentials);
        }
    }
}