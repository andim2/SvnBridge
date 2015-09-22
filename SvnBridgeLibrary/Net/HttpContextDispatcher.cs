using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using CodePlex.TfsLibrary; // NetworkAccessDeniedException
using CodePlex.TfsLibrary.ObjectModel; // TfsUtil.GetUsername()
using SvnBridge.Handlers;
using SvnBridge.Handlers.Renderers;
using SvnBridge.Infrastructure;
using SvnBridge.Infrastructure.Statistics;
using SvnBridge.Interfaces;
using SvnBridge.SourceControl; // CredentialsHelper
using System.Web;

namespace SvnBridge.Net
{
    public class HttpContextDispatcher
    {
        protected readonly IPathParser parser;
        protected readonly ActionTrackingViaPerfCounter actionTracking;

        public HttpContextDispatcher(IPathParser parser, ActionTrackingViaPerfCounter actionTracking)
        {
            this.parser = parser;
            this.actionTracking = actionTracking;
        }

        public virtual RequestHandlerBase GetHandler(string httpMethod)
        {
            switch (httpMethod.ToLowerInvariant())
            {
                case "checkout":
                    return new CheckOutHandler();
                case "copy":
                    return new CopyHandler();
                case "delete":
                    return new DeleteHandler();
                case "merge":
                    return new MergeHandler();
                case "mkactivity":
                    return new MkActivityHandler();
                case "mkcol":
                    return new MkColHandler();
                case "options":
                    return new OptionsHandler();
                case "propfind":
                    return new PropFindHandler();
                case "proppatch":
                    return new PropPatchHandler();
                case "put":
                    return new PutHandler();
                case "report":
                    return new ReportHandler();
                case "get":
                    return new GetHandler(false);
                case "head":
                    return new GetHandler(true);
                default:
                    return null;
            }
        }

        public sealed class InvalidServerUrlException : ArgumentException
        {
            public InvalidServerUrlException(string url)
                : base(string.Format("Invalid server URL \"{0}\"", url))
            {
            }
        }

        public void Dispatch(IHttpContext connection)
        {
            try
            {
                IHttpRequest request = connection.Request;
                if ("/!stats/request".Equals(request.LocalPath, StringComparison.InvariantCultureIgnoreCase))
                {
                    new StatsRenderer(Container.Resolve<ActionTrackingViaPerfCounter>()).Render(connection);
                    return;
                }

                SetupAndHandleRequest(
                    connection);
            }
            // IMPORTANT: I assume that this series of catch()es
            // is generally intended
            // to catch *any* net access issues
            // occurring *anywhere* within the *multiple* topically related parts
            // in the try scope above:
            catch (WebException ex)
            {
                actionTracking.Error();

                HttpWebResponse response = ex.Response as HttpWebResponse;

                if (response != null && response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    SendUnauthorizedResponse(connection);
                }
                else
                {
                    throw;
                }
            }
            catch (NetworkAccessDeniedException)
            {
                SendUnauthorizedResponse(connection);
            }
            catch (IOException)
            {
                // Error caused by client cancelling operation under IIS 6
                if (Configuration.LogCancelErrors)
                    throw;
            }
            catch (HttpException ex)
            {
                // Check if error caused by client cancelling operation under IIS 7
                if (!ex.Message.StartsWith("An error occurred while communicating with the remote host.") &&
                    !ex.Message.StartsWith("The remote host closed the connection."))
                    throw;

                if (Configuration.LogCancelErrors)
                    throw;
            }
        }

        private void SetupAndHandleRequest(
            IHttpContext connection)
        {
            NetworkCredential credential = null;
            try
            {
                credential = SetupPerRequestEnvironment(connection.Request);
            }
            catch (InvalidServerUrlException)
            {
                SendFileNotFoundResponse(connection);
                return;
            }

            HandleRequest(
                connection,
                credential);
        }

        /// <summary>
        /// Figures out credentials-related (whole-?)session attributes
        /// that are to be used for this particular HTTP request
        /// and assigns them to the strictly per-request-scoped RequestCache.
        /// </summary>
        private NetworkCredential SetupPerRequestEnvironment(IHttpRequest request)
        {
            NetworkCredential credential = GetCredential(request);
            string tfsUrl = parser.GetServerUrl(request, credential);
            if (string.IsNullOrEmpty(tfsUrl))
            {
                throw new InvalidServerUrlException(tfsUrl);
            }

            if (credential != null)
            {
                TweakCredential(ref credential, tfsUrl);
            }

            /// Remembering a username value here in more global scope,
            /// since a specific username identification unfortunately is required
            /// for certain services (e.g. IWorkItemModifier)
            /// which are used by the implementation of some request handlers -
            /// username *must* be a value *kept separate* from credentials handling
            /// (i.e. not dirtily inferred once one realizes that one actually needs it)
            /// since authentication handling is *NOT* always
            /// (or in fact, better should *NEVER* be)
            /// based on plaintext username ("Basic"),
            /// i.e. credentials objects are *not* (intended to be)
            /// a reliable way to gather a username!
            string domain = null;
            string username = null;
            // Now that the session credential is known,
            // try to determine the user info
            // that's potentially openly supplied by it:
            bool assumeMeaningfulAuthentication = (null != credential);
            bool needSupplyUserInfo = (assumeMeaningfulAuthentication);
            if (needSupplyUserInfo)
            {
                if (null == username)
                {
                    TryGatherSessionUserInfoFromCredential(
                        tfsUrl,
                        credential,
                        out domain,
                        out username);
                }
            }

            RequestCache.Items["serverUrl"] = tfsUrl;
            RequestCache.Items["projectName"] = parser.GetProjectName(request);
            RequestCache.Items["credentials"] = credential;

            // Decided to use more specific naming here
            // ("sessionUserDomain" rather than "domain")
            // in order to prevent in advance
            // potential Container-side mis-resolving
            // of any overly generically named and most importantly *unrelated*
            // "domain" method params of classes.
            RequestCache.Items["sessionUserDomain"] = domain;
            RequestCache.Items["sessionUserName"] = username;

            return credential;
        }

        private static NetworkCredential GetCredential(IHttpRequest request)
        {
            string authorizationHeader = request.Headers["Authorization"];
            return GetCredential(authorizationHeader);
        }

        private static NetworkCredential GetCredential(string authorizationHeader)
        {
            if (!string.IsNullOrEmpty(authorizationHeader))
            {
                // FIXME Current modus operandi:
                // we unfortunately use HTTP Basic authentication
                // (weakly plaintext-encoded MIME64-encoded username/domain/password info)
                // in order to retain access
                // to full open plaintext credentials information,
                // which then is used both to construct NetworkCredential objects
                // (which will then be used for authentication
                // against TFS web services, via NTLM etc.)
                // and to supply username info to various SvnBridge areas
                // which currently(?) rely on knowing the username.
                // Quite probably this should be fixed
                // to always [be able to] use properly securely encoded credentials
                // and to then query the user name
                // from the entity that we're successfully communicating with
                // (given the privilege that we achieved
                // after successfully having authenticated using these credentials).
                // For a good explanation, see
                // Buck Hodges "Authentication in web services with HttpWebRequest"
                //   http://blogs.msdn.com/b/buckh/archive/2004/07/28/199706.aspx
                // "Type: System.Net.NetworkCredential"
                //   http://www.cs.columbia.edu/~lok/csharp/refdocs/System.Net/types/NetworkCredential.html
                // http://stackoverflow.com/a/2528758
                if (authorizationHeader.StartsWith("Digest"))
                {
                    // FIXME: I don't think at all that this is what we want:
                    // delivery of a NetworkCredential result should be implemented
                    // by exclusively using the fully supplied Digest information only,
                    // in order to always ensure that it's the *foreign-side* SVN client user
                    // who is forced to fully authenticate,
                    // rather than just grabbing some cached credentials
                    // which might even be originating
                    // from *our* likely *different* local server-based security context.
                    //
                    // The reason that we were resorting to DefaultCredentials here
                    // might be that maybe it is not possible
                    // to create a NetworkCredential object *from*(*given*) Digest information
                    // (rather than creating an NC from username/pass
                    // which *then* is *able* to *employ* Digest authentication
                    // during communication!).


                    // However using DefaultCredentials is a SECURITY ISSUE
                    // thus it's definitely recommended to indicate
                    // that we DO NOT SUPPORT (currently cannot support?)
                    // converting Digest input data into a NetworkCredential object
                    // (unless in the SvnBridge-via-IIS case
                    // DefaultCredentials happened to *be* a credential
                    // based on having used the user's supplied Digest data!?).
                    // Clearly indicating Digest non-support
                    // is possibly best done by throwing an exception,
                    // to let the session user directly know that it was not supported.
                    // So perhaps a NetworkAccessDeniedException is best suited
                    // (or would NotSupportedException be better?).
                    // http://stackoverflow.com/a/6937030
                    // But does this exception cleanly translate
                    // into returning a specific HTTP error code
                    // which may be able to cleanly indicate "auth type not supported"?
                    // (seems this should definitely be indicated via a 401 error
                    // rather than 403).
                    // And a response quite possibly needs to supply
                    // a WWW-Authenticate header, too:
                    // http://stackoverflow.com/questions/3297048/403-forbidden-vs-401-unauthorized-http-responses#comment12449270_3297081
                    throw new NetworkAccessDeniedException();

                    //return (NetworkCredential)CredentialCache.DefaultCredentials;
                }
                else if (authorizationHeader.StartsWith("Basic"))
                {
                    string encodedCredential = authorizationHeader.Substring(authorizationHeader.IndexOf(' ') + 1);
                    string credential = UTF8Encoding.UTF8.GetString(Convert.FromBase64String(encodedCredential));
                    string[] credentialParts = credential.Split(':');

                    string username = credentialParts[0];
                    string password = credentialParts[1];

                    string domain;
                    SplitUserInfoToDomainAndName(
                        username,
                        out domain,
                        out username);
                    if (!string.IsNullOrEmpty(domain))
                    {
                        return new NetworkCredential(username, password, domain);
                    }
                    return new NetworkCredential(username, password);
                }
                else
                {
                    throw new Exception("Unrecognized authorization header: " + authorizationHeader.StartsWith("Basic"));
                }
            }
            return CredentialsHelper.NullCredentials;
        }

        /// <summary>
        /// Helper for implementing various site-specific tweaks.
        /// </summary>
        private static void TweakCredential(ref NetworkCredential credential, string tfsUrl)
        {
            // CodePlex-specific handling:
            string tfsUrlLower = tfsUrl.ToLowerInvariant();
            if ((tfsUrlLower.EndsWith("codeplex.com") || tfsUrlLower.Contains("tfs.codeplex.com")))
            {
                string username = credential.UserName;
                string domain = credential.Domain;
                if (!username.ToLowerInvariant().EndsWith("_cp"))
                {
                    username += "_cp";
                }
                if (string.IsNullOrEmpty(domain))
                {
                    domain = "snd";
                }
                credential = new NetworkCredential(username, credential.Password, domain);
            }
        }

        private static void SplitUserInfoToDomainAndName(
            string domainAndUser,
            out string domain,
            out string username)
        {
            var idxDomainSep = domainAndUser.IndexOf('\\');
            if (idxDomainSep >= 0)
            {
                domain = domainAndUser.Substring(0, idxDomainSep);
                username = domainAndUser.Substring(idxDomainSep + 1);
            }
            else
            {
                domain = "";
                username = domainAndUser;
            }
        }

        private void HandleRequest(
            IHttpContext connection,
            NetworkCredential credential)
        {
            RequestHandlerBase handler = GetHandler(connection.Request.HttpMethod);
            if (handler == null)
            {
                actionTracking.Error();
                SendUnsupportedMethodResponse(connection);
                return;
            }

            try
            {
                actionTracking.Request(handler);
                handler.Handle(
                    connection,
                    parser,
                    credential);
            }
            catch (TargetInvocationException e)
            {
                ExceptionHelper.PreserveStackTrace(e.InnerException);
                throw e.InnerException;
            }
            finally
            {
                handler.Cancel();
            }
        }

        private static void TryGatherSessionUserInfoFromCredential(
            string tfsUrl,
            NetworkCredential credential,
            out string domain,
            out string username)
        {
            domain = null;
            username = null;

            if (null != credential)
            {
                domain = credential.Domain;
                username = credential.UserName;
            }
            // This case will happen for all proper
            // (i.e. those which do *NOT* have things as plaintext information)
            // authentication types.
            if (string.IsNullOrEmpty(username))
            {
                // Last-ditch effort:
                // try to determine *any* potentially meaningful session-related username.
                //
                // Please note that generally spoken it's probably the *server* itself
                // which in its *internal implementation* knows best
                // which authenticated credentials map to which user identification,
                // i.e. this information ought to be grabbed from *server*.
                // Referencing TfsLibrary at this generic network-side place
                // probably constitutes somewhat of a layer violation,
                // but AFAICS that's the best we can do.
                string domainAndUser = TfsUtil.GetUsername(credential, tfsUrl);
                SplitUserInfoToDomainAndName(
                    domainAndUser,
                    out domain,
                    out username);
            }
        }

        private static void SendUnauthorizedResponse(IHttpContext connection)
        {
            IHttpRequest request = connection.Request;
            IHttpResponse response = connection.Response;

            response.ClearHeaders();

            response.StatusCode = (int)HttpStatusCode.Unauthorized;
            response.ContentType = "text/html; charset=iso-8859-1";

            response.AppendHeader("WWW-Authenticate", "Basic realm=\"CodePlex Subversion Repository\"");

            string content = "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                             "<html><head>\n" +
                             "<title>401 Authorization Required</title>\n" +
                             "</head><body>\n" +
                             "<h1>Authorization Required</h1>\n" +
                             "<p>This server could not verify that you\n" +
                             "are authorized to access the document\n" +
                             "requested.  Either you supplied the wrong\n" +
                             "credentials (e.g., bad password), or your\n" +
                             "browser doesn't understand how to supply\n" +
                             "the credentials required.</p>\n" +
                             "<hr>\n" +
                             "<address>" + RequestHandlerBase.GetServerIdentificationString_HostPort(request.Url.Host, request.Url.Port.ToString()) + "</address>\n" +
                             "</body></html>\n";

            AppendAsUTF8(
                response,
                content);
        }

        private static void SendUnsupportedMethodResponse(IHttpContext connection)
        {
            IHttpResponse response = connection.Response;
            response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            response.ContentType = "text/html";

            response.AppendHeader("Allow", "PROPFIND, REPORT, OPTIONS, MKACTIVITY, CHECKOUT, PROPPATCH, PUT, MERGE, DELETE, MKCOL");

            string content = @"
                <html>
                    <head>
                        <title>405 Method Not Allowed</title>
                    </head>
                    <body>
                        <h1>The requested method is not supported.</h1>
                    </body>
                </html>";

            AppendAsUTF8(
                response,
                content);
        }

        protected static void SendFileNotFoundResponse(IHttpContext connection)
        {
            IHttpResponse response = connection.Response;
            response.StatusCode = (int)HttpStatusCode.NotFound;
            response.ContentType = "text/html; charset=iso-8859-1";

            string content =
                "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n" +
                "<html><head>\n" +
                "<title>404 Not Found</title>\n" +
                "</head><body>\n" +
                "<h1>Not Found</h1>\n" +
                "<hr>\n" +
                "</body></html>\n";

            AppendAsUTF8(
                response,
                content);
        }

        private static void AppendAsUTF8(
            IHttpResponse response,
            string content)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(content);
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }
    }
}
