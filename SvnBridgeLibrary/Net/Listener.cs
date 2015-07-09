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
            TcpClient tcpClient;

            try
            {
                tcpClient = listener.EndAcceptTcpClient(asyncResult);
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            listener.BeginAcceptTcpClient(Accept, null);

            try
            {
                if (tcpClient != null)
                {
                    ProcessClient(tcpClient);
                }
            }
            catch (Exception ex)
            {
                OnListenException(ex);
            }
        }

        /// <summary>
        /// Processes the TcpClient.
        /// </summary>
        /// <param name="tcpClient">The TCP client to be processed</param>
        private void ProcessClient(TcpClient tcpClient)
        {
            IHttpContext connection = new ListenerContext(tcpClient.GetStream(), logger);
            try
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
                        connection.Request.HttpMethod,
                        connection.Request.Url.AbsoluteUri));
                }
            }
            finally
            {
                tcpClient.Close();
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

        private void LogError(Guid guid, Exception e)
        {
            logger.Error("Error on handling request. Error id: " + guid + Environment.NewLine + e.ToString(), e);
        }

        private void SendHandlerErrorResponse(
            Exception exception,
            IHttpResponse response)
        {
            response.StatusCode = 500;
            using (StreamWriter output = new StreamWriter(response.OutputStream))
            {
                Guid guid = Guid.NewGuid();

                string message = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                                 "<D:error xmlns:D=\"DAV:\" xmlns:m=\"http://apache.org/dav/xmlns\" xmlns:C=\"svn:\">\n" +
                                 "<C:error/>\n" +
                                 "<m:human-readable errcode=\"160024\">\n" +

                                 ("Failed to process a request. Failure id: " + guid + "\n" + exception) +

                                 "</m:human-readable>\n" +
                                 "</D:error>\n";
                output.Write(message);

                LogError(guid, exception);
            }
        }

        private void OnListenException(Exception ex)
        {
            Helper.DebugUsefulBreakpointLocation();
            ListenError(this, new ListenErrorEventArgs(ex));
        }
    }
}
