using System;
using System.Diagnostics; // Debug.WriteLine()
using System.IO;
using System.Net;
using System.Net.Sockets;
using SvnBridge.Infrastructure;
using SvnBridge.Infrastructure.Statistics;
using SvnBridge.Interfaces;

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

        private static event EventHandler<ListenErrorEventArgs> ErrorOccured = delegate { };

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
            ErrorOccured += OnErrorOccured;
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
            bool useSecureMode = true;

            if (!useSecureMode)
            {
                Debug.WriteLine("Attention: SVN network listener configured to provide insecure global (not loopback-only) network interface bind.\n");
            }

            bool doLocalhostInterfaceBindOnly = useSecureMode;

            IPAddress ipAddressToBindTo = doLocalhostInterfaceBindOnly ? IPAddress.Loopback : IPAddress.Any;
            return ipAddressToBindTo;
        }

        private void OnErrorOccured(object sender, ListenErrorEventArgs e)
        {
            OnListenException(e.Exception);
        }

        public virtual void Stop()
        {
            listener.Stop();
            ErrorOccured -= OnErrorOccured;
			
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
                    Process(tcpClient);
                }
            }
            catch (Exception ex)
            {
                OnListenException(ex);
            }
        }

        private void Process(TcpClient tcpClient)
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
                		connection.Response.StatusCode = 500;
                		using (StreamWriter sw = new StreamWriter(connection.Response.OutputStream))
                		{
                			Guid guid = Guid.NewGuid();

                			string message = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                			                 "<D:error xmlns:D=\"DAV:\" xmlns:m=\"http://apache.org/dav/xmlns\" xmlns:C=\"svn:\">\n" +
                			                 "<C:error/>\n" +
                			                 "<m:human-readable errcode=\"160024\">\n" +
                                    
                			                 ("Failed to process a request. Failure id: " + guid + "\n" + exception) +

                			                 "</m:human-readable>\n" +
                			                 "</D:error>\n";
                			sw.Write(message);

                			LogError(guid, exception);
                		}
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

        private void OnListenException(Exception ex)
        {
            ListenError(this, new ListenErrorEventArgs(ex));
        }

        public static void RaiseErrorOccured(Exception e)
        {
            
        }
    }
}
