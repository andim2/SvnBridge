using System;
using System.Diagnostics; // Debug.WriteLine()
using System.IO;
using System.Net;
using System.Net.Sockets;
using SvnBridge.Infrastructure;
using SvnBridge.Infrastructure.Statistics;
using SvnBridge.Interfaces;
using SvnBridge.Utility; // Helper.DebugUsefulBreakpointLocation()

namespace SvnBridge.Net
{
    public class Listener
    {
        private HttpContextDispatcher dispatcher;
        private bool isListening;
        private readonly DefaultLogger logger;
        private TcpListener listener;
        // Convenient config setting bool -
        // indicates whether SvnBridge wants to support
        // HTTP Keep-Alive (aka "HTTP persistent connection" / "connection reuse") or not.
        private readonly bool supportHttpKeepAlive;

        private int? port;
        private ActionTrackingViaPerfCounter actionTracking;

        public Listener(DefaultLogger logger, ActionTrackingViaPerfCounter actionTracking)
        {
            this.logger = logger;
            this.supportHttpKeepAlive = true;
            this.actionTracking = actionTracking;
        }

        private static event EventHandler<ListenErrorEventArgs> ErrorOccurred = delegate { };

        public virtual event EventHandler<ListenErrorEventArgs> ListenError = delegate { };
        public virtual event EventHandler<FinishedHandlingEventArgs> FinishedHandling = delegate { };

        public virtual int Port
        {
            get { return port.GetValueOrDefault(); }
            set
            {
                if (isListening)
                {
                    throw new InvalidOperationException("The port cannot be changed while the listener is listening.");
                }

                port = value;
            }
        }

        public virtual void Start(IPathParser parser)
        {
            if (!port.HasValue)
            {
                throw new InvalidOperationException("A port must be specified before starting the listener.");
            }
            ErrorOccurred += OnErrorOccurred;
            dispatcher = new HttpContextDispatcher(parser, actionTracking);

            isListening = true;
            listener = new TcpListener(DetermineNonPublicInterfaceBindAddress(), Port);
            listener.Start();

            // Initial async BeginAccept done in main thread,
            // all subsequent ones do *not* need to be performed by main thread
            // (done by the worker callbacks, which temporarily suspend Accept()ing).
            listener.BeginAcceptTcpClient(Accept, null);
        }

        private static IPAddress DetermineNonPublicInterfaceBindAddress()
        {
            // CHANGE THIS IF NEEDED!
            // (if you need to allow the desktop SvnBridge
            // to offer its service externally,
            // on ethernet network interface etc.).
            // Convenient search keywords: interface bind ip port firewall localhost external
            // Note: THIS SETTING MIGHT OBVIOUSLY CONSTITUTE A SECURITY ISSUE!
            // (and this is especially the case since a NetworkCredential as used by SvnBridge
            // sometimes might be one of the current possibly more privileged security context
            // rather than an authentication-enforcing foreign user-side one!)
            // We currently don't offer
            // (and given that this setting is a security issue we likely shouldn't)
            // a setting at the Settings GUI presenter (yet?).
            // So at the moment to enable this setting
            // you will have to cause a setting change in GUI,
            // then edit the saved (due to setting change) .config file,
            // to manually add the config entry.
            bool useSecureMode = (true != Configuration.NetworkSvnUseInsecureNonLoopbackBind);

            if (!useSecureMode)
            {
                Debug.WriteLine("Attention: SVN network listener configured to provide insecure global (not loopback-only) network interface bind, due to NetworkSvnUseInsecureNonLoopbackBind app.config setting.\n");
            }

            bool doLocalhostInterfaceBindOnly = useSecureMode;

            IPAddress ipAddressToBindTo = doLocalhostInterfaceBindOnly ? IPAddress.Loopback : IPAddress.Any;
            return ipAddressToBindTo;
        }

        private void OnErrorOccurred(object sender, ListenErrorEventArgs e)
        {
            OnListenException(e.Exception);
        }

        public virtual void Stop()
        {
            listener.Stop();
            ErrorOccurred -= OnErrorOccurred;
			
            isListening = false;
        }

        private void Accept(IAsyncResult asyncResult)
        {
            try
            {
                ServeNewClientAtListener(asyncResult);
            }
            catch (ObjectDisposedException e)
            {
                bool suspectKnownCaseOfCanceledListener = IsThisObjectDisposedExceptionTypeDueToCanceledListener(
                    listener,
                    e);
                bool isKnownBenignCase = (suspectKnownCaseOfCanceledListener);
                bool needSilenceException = (isKnownBenignCase);
                bool doNormalExceptionRethrow = !(needSilenceException);
                if (!(doNormalExceptionRethrow))
                {
                    // Need to actively bail out (return) here
                    // (even if this was the known exception case to be silenced,
                    // we still cannot continue normally further below).
                    return;
                }

                throw;
            }
        }

        private void ServeNewClientAtListener(IAsyncResult asyncResult)
        {
            // For every BeginAcceptTcpClient() call, code implementation
            // should *unconditionally* ensure that a symmetric call to End...() happens
            // (otherwise necessary cleanup might get skipped).
            // In the listener.Stop() case,
            // the IAsyncResult callback will actually be called
            // (allows call to End...())
            // yet the listener will not provide a tcpClient
            // (throws ObjectDisposedException).
            using (var tcpClient = listener.EndAcceptTcpClient(asyncResult))
            {
                // Now directly resume accepting a new incoming client
                // (accepting new clients should be reenabled ASAP, obviously;
                // and definitely *unconditionally* reenable accepting clients,
                // even if *current* client instance happened to turn out null):
                listener.BeginAcceptTcpClient(Accept, null);

                if (tcpClient != null)
                {
                    try
                    {
                        ProcessClient(tcpClient);
                    }
                    catch (Exception ex)
                    {
                        OnListenException(ex);
                    }
                    finally
                    {
                          // Should not be necessary ("using" tcpClient above):
                          //TcpClientClose(tcpClient);
                    }
                }
            }
        }

        /// <summary>
        /// Almost comment-only helper.
        /// </summary>
        /// <remarks>
        /// Since silencing an ObjectDisposedException
        /// is considered a dirty thing to do,
        /// for those cases where we do need to silence them sometimes,
        /// at least ensure that we silence the exception only in case
        /// it strictly was due to the known "benign" EndAcceptTcpClient() case
        /// (ObjectDisposedException due to listener having been canceled):
        /// ensure this case by:
        /// - doing related Listener handling
        ///   within a very restrictively scoped (EndAcceptTcpClient()-focussed) try { }
        /// - doing special .IsBound check that exception was indeed due to this case
        /// [BTW, one could circumvent this "undesirable" Listener-produced exception
        /// by doing .IsBound check prior to EndAcceptTcpClient() call,
        /// but that would produce at least two (very) undesirable things:
        /// move exceptional handling out of error path
        /// *and* I suspect it would introduce a race condition (open a race window) -
        /// DO NOT do an advance check of whether some action "may fail" and then skip execution,
        /// but rather DO actively do it and *then* handle errors
        /// only in case they *do* turn up -
        /// see also the very comparable fugly case of various filesystem-side file existence check race conditions [mktemp etc.]).
        /// http://stackoverflow.com/questions/1173774/stopping-a-tcplistener-after-calling-beginaccepttcpclient#comment12623099_1174002
        /// Side note: we also end up with this behaviour
        /// when reconfiguring Settings of the running program, to use a different port.
        /// </remarks>
        private static bool IsThisObjectDisposedExceptionTypeDueToCanceledListener(
            TcpListener listener,
            ObjectDisposedException e)
        {
            bool isExceptionDueToCanceledListener = false;

            bool isServerBound = (listener.Server.IsBound);

            if (!(isServerBound))
            {
                isExceptionDueToCanceledListener = true;
            }

            return isExceptionDueToCanceledListener;
        }

        /// <summary>
        /// Keeps processing the TcpClient until it's fully completed, i.e.
        /// either it ended up happy (success)
        /// since all (potentially multiple: Keep-Alive) requests
        /// are fully served
        /// or an error occurred,
        /// e.g. exceptions such as timeout or socket close.
        /// </summary>
        /// <param name="tcpClient">The TCP client to be processed</param>
        private void ProcessClient(TcpClient tcpClient)
        {
            using (var networkStream = tcpClient.GetStream())
            {
                try
                {
                    ProcessClientStream(
                        networkStream);
                }
                catch(IOException ex)
                {
                    bool needSilenceException = NeedSilenceThisIOExceptionTypeForClientProcessing(
                        ex);

                    if (!(needSilenceException))
                    {
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// (Almost) comment-only helper.
        /// </summary>
        /// <remarks>
        /// For the Keep-Alive case we have code to keep listening
        /// for potentially incoming additional requests,
        /// thus we actively choose to work against a receive timeout
        /// which *will* cause IOException on timeout (i.e. no more requests arriving).
        /// Thus ignore IOExceptions in case of Keep-Alive, but only in that case!!
        /// http://stackoverflow.com/questions/3066404/streamreader-endofstream-produces-ioexception
        /// http://stackoverflow.com/questions/2652496/how-long-will-networkstream-read-wait-before-dying
        /// http://stackoverflow.com/questions/5579498/how-to-stop-a-blocking-streamreader-endofstream-on-a-networkstream
        /// http://stackoverflow.com/questions/1361714/how-do-you-wait-for-a-network-stream-to-have-data-to-read
        /// </remarks>
        private bool NeedSilenceThisIOExceptionTypeForClientProcessing(
            IOException ex)
        {
            bool needSilenceException = false;

            if (supportHttpKeepAlive)
            {
                if (IsIOExceptionOfTypeUnableToReadData(
                    ex))
                {
                    needSilenceException = true;
                }
            }

            return needSilenceException;
        }

        /// <summary>
        /// Helper to try to contribute to properly telling apart
        /// distinctly different yet woefully indiscernible types of IOException:s.
        /// </summary>
        /// <remarks>
        /// Related:
        /// http://stackoverflow.com/questions/5638231/socketchannel-java-io-ioexception-an-existing-connection-was-forcibly-closed-b
        /// </remarks>
        private static bool IsIOExceptionOfTypeUnableToReadData(
            IOException ex)
        {
            return ex.Message.StartsWith("Unable to read data from the transport connection");
        }

        /// <remarks>
        /// Provide both ProcessClient() and ProcessClientStream() methods,
        /// to keep processing nicely and cleanly sub scoped.
        /// </remarks>
        private void ProcessClientStream(
            NetworkStream networkStream)
        {
            // Subversion neon-debug-mask 511 indicated that Subversion was surprised about an interim socket close
            // via its "Could not read status line" / "Persistent connection timed out, retrying" log
            // despite requesting (and formerly being falsely acknowledged!) HTTP Keep-Alive
            // (side note: Keep-Alive became the default mechanism in HTTP/1.1).
            // Root cause probably is our Net Listener setup being based on IHttpRequest rather than
            // a full HttpWebRequest, i.e. we're rolling our own implementation.
            // Thus make sure to support Keep-Alive properly. Note that we don't support the pipelining possibilities of Keep-Alive (yet?).
            // As a side effect, enabling persistent connections to the client side (SVN)
            // seems to have provided some relief to the hairy socket exhaustion exception issue as well.
            //
            // Some search keywords: "ServicePoint", "HttpBehaviour", "DefaultConnectionLimit", "DefaultPersistentConnectionLimit".

            bool requestedHttpKeepAlive = false; // globally persistent state (KEEP SCOPE OUT OF LOOP)
            for (int numRequestsHandled = 0; ; ++numRequestsHandled)
            {
                bool wasSuccessfulRequest = TryHandleOneHttpMethodRequest(
                    networkStream,
                    ref requestedHttpKeepAlive);

                if (!(wasSuccessfulRequest))
                {
                    break;
                }

                // This multi-request loop should not be implemented via
                // an overly simplistic DataAvailable conditional - rather,
                // it probably should be infinite i.e. terminated only by hitting ReceiveTimeout / client-side socket close.
                // Also, note that e.g. TcpClient.Connected is said to be unreliable:
                // http://go4answers.webhost4life.com/Example/net4-tcpclient-fails-81091.aspx
                // Possibly relevant: http://stackoverflow.com/a/1980554/1222997
                //   "The HTTP protocol has the status code Request Timeout which you can send to the client if it seems dead."
                if (!(requestedHttpKeepAlive))
                {
                    break;
                }
            }
        }

        private bool TryHandleOneHttpMethodRequest(
            NetworkStream networkStream,
            ref bool requestedHttpKeepAlive)
        {
            IHttpContext context = new ListenerContext(
                networkStream,
                logger);

            bool isGoodHttpRequest = IsGoodHttpRequest(
                context.Request);

            bool canHandlePerHttpMethodRequestContext = false;
            if (isGoodHttpRequest)
            {
                canHandlePerHttpMethodRequestContext = true;
            }

            if (!(canHandlePerHttpMethodRequestContext))
                return false;

            HandlePerHttpMethodRequestContext(
                context,
                networkStream,
                ref requestedHttpKeepAlive);

            return true;
        }

        private void HandlePerHttpMethodRequestContext(
            IHttpContext context,
            NetworkStream networkStream,
            ref bool requestedHttpKeepAlive)
        {
            var response = context.Response;
            bool doSupportHttpKeepAlive = (supportHttpKeepAlive);
            // See also http://www.w3.org/Protocols/HTTP/Issues/http-persist.html
            // http://stackoverflow.com/questions/140765/how-do-i-know-when-to-close-an-http-1-1-keep-alive-connection?rq=1
            if (doSupportHttpKeepAlive)
            {
                // In case it's not known yet in this chain of HTTP requests
                // whether client wants Keep-Alive, evaluate it and do setup if necessary.
                // FIXME: Wikipedia "In HTTP 1.1, all connections are considered persistent unless declared otherwise".
                // Problem here is that ListenerRequest ParseStartLine() currently
                // does not provide the HTTP version value yet...
                if (!(requestedHttpKeepAlive))
                {
                    bool foundKeepAlive;
                    GetHttpHeaderConnectionConfig(
                        context.Request,
                        out foundKeepAlive);

                    if (foundKeepAlive)
                    {
                        requestedHttpKeepAlive = true;
                    }
                    bool setupHttpKeepAlive = (requestedHttpKeepAlive);
                    if (setupHttpKeepAlive)
                    {
                        int httpKeepAliveTimeoutSec;
                        int httpKeepAliveMaxConnections;
                        GetHttpKeepAliveSettings(
                            out httpKeepAliveTimeoutSec,
                            out httpKeepAliveMaxConnections);

                        //tcpClient.Client.Blocking = true;
                        // ".Net 2.0 Breaks .Net 1.1 TcpClient ReadTimeouts"
                        //   http://social.msdn.microsoft.com/forums/en-US/netfxnetcom/thread/d3769a76-3b4a-4b80-b601-055da5370627/
                        // says: "You must set NetworkStream.ReadTimeout, NEVER set TcpClient.ReceiveTimeout"
                        //tcpClient.ReceiveTimeout = httpKeepAliveTimeoutSec * 1000;
                        networkStream.ReadTimeout = httpKeepAliveTimeoutSec * 1000;
                        StringWriter writer = new StringWriter();
                        writer.Write("timeout={0}, max={1}", httpKeepAliveTimeoutSec, httpKeepAliveMaxConnections);
                        response.AppendHeader("Keep-Alive", writer.ToString());
                    }
                }
            }
            else
            {
                ConnectionIndicateNonPersistent(
                    response);
            }

            // Now do actual handling
            // of the currently requested HTTP method:
            HandleOneHttpRequest(
                context);
        }

        /// <summary>
        /// Comment-only helper.
        ///
        /// Hmm... this check has been added to detect non-existing requests
        /// due to e.g. the client having closed the socket
        /// within the (obviously non-existing) next request that we attempt to fetch (HTTP Keep-Alive).
        /// Rather than doing an explicit open-coded check here,
        /// an alternative might be to do things exception-based
        /// (have internal handler input processing throw exception on closed socket [NetworkStream.Read() == 0]) -
        /// after all connection object isn't usable anyway, thus it should probably be handled via exception,
        /// to cleanly unwind all inner parts.
        /// OTOH socket closing will always happen,
        /// i.e. it is an expected (completely non-exceptional) event,
        /// thus it should not be handled via exception after all.
        /// </summary>
        private static bool IsGoodHttpRequest(
            IHttpRequest request)
        {
            bool isGoodHttpRequest = false;

            bool isValidHttpMethod = !string.IsNullOrEmpty(request.HttpMethod);
            if (isValidHttpMethod)
            {
                isGoodHttpRequest = true;
            }

            return isGoodHttpRequest;
        }

        private static void GetHttpHeaderConnectionConfig(
            IHttpRequest request,
            out bool foundKeepAlive)
        {
            foundKeepAlive = false;

            string connectionHeader = request.Headers["Connection"];
            if (null != connectionHeader)
            {
                ParseHttpHeaderConnection(
                    connectionHeader,
                    out foundKeepAlive);
            }
        }

        private static void ParseHttpHeaderConnection(
            string connectionHeader,
            out bool foundKeepAlive)
        {
            foundKeepAlive = false;

            string[] connectionHeaderParts = connectionHeader.Split(',');
            foreach (string directive in connectionHeaderParts)
            {
                string directiveStart = directive.TrimStart();
                bool isDirectiveKeepAlive = directiveStart.Equals("Keep-Alive", StringComparison.OrdinalIgnoreCase);
                if (isDirectiveKeepAlive)
                {
                    foundKeepAlive = true;
                    break;
                }
            }
        }

        /// <remarks>
        /// http://blog.fastmail.fm/2011/06/28/http-keep-alive-connection-timeouts/
        /// says that some firewalls have a 2 minute state timeout
        /// --> might want to safely stay quite a bit below this value.
        ///
        /// References:
        /// http://stackoverflow.com/questions/4139379/http-keep-alive-in-the-modern-age
        /// http://stackoverflow.com/questions/8222987/enable-keep-alive-page-speed
        /// </remarks>
        private static void GetHttpKeepAliveSettings(
            out int httpKeepAliveTimeoutSec,
            out int httpKeepAliveMaxConnections)
        {
            httpKeepAliveTimeoutSec = GetHttpKeepAliveTimeoutSeconds();
            httpKeepAliveMaxConnections = GetHttpKeepAliveMaxConnections();
        }

        /// <summary>
        /// Helper to decide on a convenient yet safe
        /// HTTP KA timeout value.
        /// </summary>
        private static int GetHttpKeepAliveTimeoutSeconds()
        {
            int httpKeepAliveTimeoutSec;

            httpKeepAliveTimeoutSec = 45;

            return httpKeepAliveTimeoutSec;
        }

        /// <remarks>
        /// TODO: specs seem to indicate
        /// that when the server does report a max value for connections,
        /// it is free to end serving requests after that number of requests were done,
        /// so perhaps we should replace the request loop condition
        /// with a counter of 1 in non-Keep-Alive case
        /// and maxConn in Keep-Alive case.
        /// </remarks>
        private static int GetHttpKeepAliveMaxConnections()
        {
            return 100;
        }

        private static void ConnectionIndicateNonPersistent(
            IHttpResponse response)
        {
            // Hmm, despite us advertising "close", Subversion 1.6.17 still attempts persistent connections.
            // This as observed on SvnBridge/.NET 2.0.5xxx.
            // Hmm, HTTP 1.1 is specified to have persistence by default,
            // so possibly this is because of Subversion not obeying us manually disabling persistence...
             // It *seems* HTTP header values are supposed to be treated case-insensitively,
            // however "close" is spelt lower-case in most cases,
            // thus write it in the more common variant:
            response.AppendHeader("Connection", "close");
        }

        /// <remarks>
        /// Most likely a lot of HTTP-specific handling here
        /// (including all that HTTP Keep-Alive setup)
        /// ought to be moved to the post-Dispatch() inner side.
        /// However, this would mean
        /// that IHttpContext would become a connection-global
        /// (persisting through all HTTP requests within Keep-Alive)
        /// rather than a per-request object.
        /// But in fact this seems to be what it should actually be,
        /// judging from the HttpContext implementation of fap.googlecode.com's HttpContext.cs.
        /// SendHandlerErrorResponse() would then (mostly)
        /// be moved inside HTTP dispatcher impl as well.
        /// UPDATE: Hmm, this is not how we currently implement Keep-Alive feature
        /// (we don't keep IHttpContext object during all HTTP method requests,
        /// but re-create it per-method-request,
        /// which actually may or may not be how it is supposed to be done).
        /// </remarks>
        private void HandleOneHttpRequest(
            IHttpContext connection)
        {
            DateTime timeUtcStart = DateTime.UtcNow;
            try
            {
                // The global RequestCache object
                // currently(?) seems to store
                // several *per-request-restricted* attributes
                // (attributes that are *specific*
                // to the particular HTTP method
                // that is about to be processed),
                // thus we'll have to init/tear down it
                // here (and here at this scope only!!)
                // anew.
                RequestCache.Init();
                dispatcher.Dispatch(connection);
            }
            catch (Exception exception)
            {
                try
                {
                    SendHandlerErrorResponse(
                        exception,
                        connection.Response);
                }
                catch
                {
                    // we explicitly ignore all exceptions here, we don't really have
                    // much to do if the error handling code failed to work, after all.
                }
                // we still raise the original exception (from further above), though.
                throw;
            }
            finally
            {
                RequestCache.Dispose();
                FlushConnection(connection);
                TimeSpan duration = DateTime.UtcNow - timeUtcStart;
                FinishedHandling(this, new FinishedHandlingEventArgs(duration,
                    connection.Request.Url.AbsoluteUri,
                    connection.Request.HttpMethod));
            }
        }

        /// <summary>
        /// I guess that this "dirty" (non-using-scoped) handler
        /// was intentionally added this way
        /// since right upon Flush()ing
        /// there may be this corresponding IOException occurring
        /// which needs to be ignored at this very scope -
        /// otherwise scope of catching (various unknown-reason) IOException:s
        /// would be way too imprecise...
        /// </summary>
        private static void FlushConnection(IHttpContext connection)
        {
            try
            {
                connection.Response.OutputStream.Flush();
            }
            catch (IOException)
            {
                /* Ignore error, caused by client cancelling operation */
            }
        }

        /// <summary>
        /// Comment-only helper.
        /// </summary>
        /// <remarks>
        /// side note: Close()ing TcpClient
        /// will *not* Close() its NetworkStream,
        /// see GetStream() docs (http://stackoverflow.com/a/12691758).
        /// </remarks>
        private static void TcpClientClose(TcpClient tcpClient)
        {
            tcpClient.Close();
        }

        private void LogError(Guid guid, Exception e)
        {
            logger.Error("Error on handling request. Error id: " + guid + Environment.NewLine + e.ToString(), e);
        }

        private void SendHandlerErrorResponse(
            Exception exception,
            IHttpResponse response)
        {
            Guid guid = Guid.NewGuid();

            response.StatusCode = 500;
            using (StreamWriter output = new StreamWriter(response.OutputStream))
            {
                string message = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                                 "<D:error xmlns:D=\"DAV:\" xmlns:m=\"http://apache.org/dav/xmlns\" xmlns:C=\"svn:\">\n" +
                                 "<C:error/>\n" +
                                 "<m:human-readable errcode=\"160024\">\n" +

                                 ("Failed to process a request. Failure id: " + guid + "\n" + exception) +

                                 "</m:human-readable>\n" +
                                 "</D:error>\n";
                output.Write(message);
            }

            LogError(guid, exception);
        }

        private void OnListenException(Exception ex)
        {
            Helper.DebugUsefulBreakpointLocation();
            ListenError(this, new ListenErrorEventArgs(ex));
        }
    }
}
