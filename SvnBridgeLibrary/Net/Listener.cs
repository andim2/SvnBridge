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
        private int? port;
        private ActionTrackingViaPerfCounter actionTracking;

        public Listener(DefaultLogger logger, ActionTrackingViaPerfCounter actionTracking)
        {
            this.logger = logger;
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
        /// Processes the TcpClient.
        /// </summary>
        /// <param name="tcpClient">The TCP client to be processed</param>
        private void ProcessClient(TcpClient tcpClient)
        {
            using (var networkStream = tcpClient.GetStream())
            {
                ProcessClientStream(
                    networkStream);
            }
        }

        /// <remarks>
        /// Provide both ProcessClient() and ProcessClientStream() methods,
        /// to keep processing nicely and cleanly sub scoped.
        /// </remarks>
        private void ProcessClientStream(
            NetworkStream networkStream)
        {
            IHttpContext connection = new ListenerContext(
                networkStream,
                logger);

            HandleConnection(
                connection);
        }

        private void HandleConnection(
            IHttpContext connection)
        {
            HandleOneHttpRequest(
                connection);
        }

        private void HandleOneHttpRequest(
            IHttpContext connection)
        {
            DateTime start = DateTime.Now;
            try
            {
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
                // we still raise the original exception, though.
                throw;
            }
            finally
            {
                RequestCache.Dispose();
                FlushConnection(connection);
                TimeSpan duration = DateTime.Now - start;
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
