using System;
using System.Net;
using SvnBridge.Interfaces;
using SvnBridge.Infrastructure;

namespace SvnBridge.PathParsing
{
	public class PathParserServerAndProjectInPath : BasePathParser
	{
		private readonly TfsUrlValidator urlValidator;

		public PathParserServerAndProjectInPath(TfsUrlValidator urlValidator)
		{
			this.urlValidator = urlValidator;
		}

		public override string GetServerUrl(IHttpRequest request, ICredentials credentials)
		{
			string url = GetUrlFromRequest(request.Url);

        string strTfsBase = c_strTfs;

        string strTest;
        strTest = c_strHTTPS + url + c_strPort8443 + strTfsBase;
            if (urlValidator.IsValidTfsServerUrl(strTest))
                return strTest;
        strTest = c_strHTTP + url + c_strPort8080 + strTfsBase;
            if (urlValidator.IsValidTfsServerUrl(strTest))
                return strTest;
        strTest = c_strHTTPS + url;
            if (urlValidator.IsValidTfsServerUrl(strTest))
                return strTest;
        strTest = c_strHTTP + url;
            if (urlValidator.IsValidTfsServerUrl(strTest))
                return strTest;
        strTest = c_strHTTP + url + c_strPort8080;
            if (urlValidator.IsValidTfsServerUrl(strTest))
                return strTest;
        strTest = c_strHTTPS + url + c_strPort8443;
            if (urlValidator.IsValidTfsServerUrl(strTest))
                return strTest;
            return c_strHTTP + url;
		}

		private string GetUrlFromRequest(Uri requestUrl)
		{
			string path = requestUrl.GetComponents(UriComponents.Path, UriFormat.SafeUnescaped);
            
            if (string.IsNullOrEmpty(path))
            {
                throw new InvalidOperationException("Could not find server url in the url (" +
                    requestUrl.AbsoluteUri + "). Not valid when using the RequestBasePathParser");
            }

        string authorityPlusTfsServiceBasePath;
        string tfsTeamProjPath;
        SplitTFSServiceAndTeamProjParts(path, out authorityPlusTfsServiceBasePath, out tfsTeamProjPath);
        return authorityPlusTfsServiceBasePath;
    }

    private static void SplitTFSServiceAndTeamProjParts(string path, out string authorityPlusTfsServiceBasePath, out string tfsTeamProjPath)
    {
        // When using TFS installations with /tfs/DefaultCollection suffix,
        // it's very important to specify the TFS Team project sub part
        // via the proper magic team project syntax
        // (otherwise we'll have to do a problematic last-ditch effort
        // to try to figure out the TFS service base URL).
        string delimTFSTeamProjectRootPrefixMagic = TFSTeamProjectRootPrefixMagic;
        string delimTfsTeamProjPart = "/" + delimTFSTeamProjectRootPrefixMagic;
        int serverDelimIndex = SplitString(path, delimTfsTeamProjPart, out authorityPlusTfsServiceBasePath, out tfsTeamProjPath);
        bool foundDelim = (-1 != serverDelimIndex);
        if (!foundDelim)
        {
            string delimRawPath = "/";
            SplitString(path, delimRawPath, out authorityPlusTfsServiceBasePath, out tfsTeamProjPath);
        }
		}

		public override string GetLocalPath(IHttpRequest request)
		{
			return GetLocalPath(request.Url.AbsoluteUri);
		}

		public override string GetLocalPath(IHttpRequest request, string url)
		{
			return GetLocalPath(url);
		}

		private string GetLocalPath(string url)
		{
            // If a relative url has been provided, make it an absolute URL so we can still
            // get the same unescaped path from it.
            if (url.StartsWith("/"))
            {
                url = "http://FakeHost" + url;
            }

			Uri urlAsUri = new Uri(url);
			string path = urlAsUri.GetComponents(UriComponents.Path, UriFormat.SafeUnescaped);
      string authorityPlusTfsServiceBasePath;
      string tfsTeamProjPath;
      SplitTFSServiceAndTeamProjParts(path, out authorityPlusTfsServiceBasePath, out tfsTeamProjPath);
      string localPath = tfsTeamProjPath;
			if (!localPath.StartsWith("/"))
				localPath = "/" + localPath;

			return localPath;
		}

		public override string GetProjectName(IHttpRequest request)
		{
			return null;
		}

		public override string GetApplicationPath(IHttpRequest request)
		{
			string url = GetUrlFromRequest(request.Url);
			string path = url + request.ApplicationPath;
			if (path.StartsWith("/") == false)
				path = '/' + path;
			if (path.EndsWith("/") == false)
				path = path + '/';
            if (path.Contains(c_strTfs))
                path += TFSTeamProjectRootPrefixMagic;
			return path;
		}

		public override string GetPathFromDestination(string href)
		{
			return GetLocalPath(href);
		}
	}
}
