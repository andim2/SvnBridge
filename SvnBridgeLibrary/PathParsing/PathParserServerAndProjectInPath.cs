using System;
using System.Net;
using SvnBridge.Interfaces;
using SvnBridge.Infrastructure;
using SvnBridge.Utility; // Helper.PercentEncodeConditional()

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
			return GetBaseUrlOfTFSService(request, credentials);
		}

    /// <summary>
    /// Figures out the base URL where a TFS service can be reached
    /// (i.e. below which a particular sub page
    /// [e.g. Services/v1.0/Registration.asmx]
    /// is provided).
    /// Will iterate through several https/http URLs
    /// with certain corresponding port numbers.
    /// </summary>
    private string GetBaseUrlOfTFSService(IHttpRequest request, ICredentials credentials)
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
        //
        // HOWEVER, note that parsing of this delimiter is problematic:
        // while subversion sends the "$/" part non-percent-encoded
        // (since "$" is NOT described
        // as a "reserved" and thus required-encoded part
        // of the URI's path component by RFC3986!),
        // git-svn decides to needlessly percent-encode ("PE") it into "%24/".
        // Unfortunately Uri.GetComponents(UriComponents.Path, UriFormat.SafeUnescaped)
        // then decides to NOT PU it
        // despite it being a non-reserved (i.e., only *optionally* PE:d) pchar,
        // AND MSDN documenting a *specific list* of reserved chars
        // which it will NOT unescape: "%", "#", "?", "/", "\", and "@" -
        // yet "$" is *NOT* listed here yet is *NOT* unescaped!! ARGH!!!
        // (this seems to be governed by the internal UriExt.cs method IsNotSafeForUnescape()).
        // This strange behaviour might be related to Uri's setting of IDN/IRI parsing handling
        // (see .AllowIdn member etc.), but that seems to not be influencable
        // (at least not flexibly enough).
        //
        // Since the '$' part *may* be PE:d,
        // but other *reserved* URI parts should definitely NOT be PU:d by us,
        // we'll better handle things in a clever inverted way
        // by only PE:ing the part which *we* need to compare against,
        // rather than dangerously PU:ing (--> potentially corrupting!!)
        // the entire incoming URI's path part!
        string delimTFSTeamProject = TFSTeamProjectSeparator;
        string delimTfsTeamProjPart = "/" + delimTFSTeamProject;
        int serverDelimIndex = SplitString(path, delimTfsTeamProjPart, out authorityPlusTfsServiceBasePath, out tfsTeamProjPath);
        bool foundDelim = (-1 != serverDelimIndex);
        if (!foundDelim)
        {
            // Hmm, we CANNOT PE TFSTeamProjectSeparator in its entirety,
            // since that would PE its '/' part, too!!
            // (which the incoming path does *NOT* have PE:d, IOW we would not find a match!)
            // Thus, let's PE the '$' only (and avoid implicit knowledge of specific TFS magic parts
            // by passing it in its entirety to the percent-encoder, specifying that "/" should NOT be encoded!),
            // to then be able to string-replace such PE:d parts back to the '$' sign.
            string delimTfsTeamProjPart_PE = Helper.PercentEncodeConditional(delimTfsTeamProjPart, null, "/");
            serverDelimIndex = SplitString(path, delimTfsTeamProjPart_PE, out authorityPlusTfsServiceBasePath, out tfsTeamProjPath);
            foundDelim = (-1 != serverDelimIndex);
        }
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
                path += TFSTeamProjectSeparator;
			return path;
		}

		public override string GetPathFromDestination(string href)
		{
			return GetLocalPath(href);
		}
	}
}
