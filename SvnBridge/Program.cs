using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using CodePlex.TfsLibrary.RegistrationWebSvc;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.Infrastructure;
using SvnBridge.Net;
using SvnBridge.Presenters;
using SvnBridge.Properties;
using SvnBridge.Utility;
using SvnBridge.Views;

namespace SvnBridge
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
        	Logging.TraceEnabled = Settings.Default.TraceEnabled;
            Logging.MethodTraceEnabled = false;

            BootStrapper.Start();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            int? port;

            if (args.Length > 0)
            {
                ushort tmp;
                if (ushort.TryParse(args[0], out tmp))
                {
                    port = tmp;
                }
                else
                {
                    MessageBox.Show("Could not parse port: " + args[0] +
                                    ". If the port is explicitly specified it must be numeric 0 - 65536");
                    return;
                }
            }
            else
            {
                port = TryGetPortFromSettings();
            }
            ProxyInformation proxyInfo = GetProxyInfo();

            bool hasPortFromRequest =
                port != null &&
                Helper.IsPortInUseOnLocalHost(port.Value) == false;

            if (hasPortFromRequest || TryGetSettings(ref port, proxyInfo))
            {
                Run(port ?? 8081, proxyInfo);
            }
        }

        private static int? TryGetPortFromSettings()
        {
            if (Settings.Default.TfsPort != 0)
                return Settings.Default.TfsPort;
            return null;
        }

        public static ProxyInformation GetProxyInfo()
        {
            var proxyInfo = new ProxyInformation();
            proxyInfo.UseProxy = Settings.Default.UseProxy;
            proxyInfo.Url = Settings.Default.ProxyUrl;
            proxyInfo.Port = Settings.Default.ProxyPort;
            proxyInfo.UseDefaultCredentails = Settings.Default.ProxyUseDefaultCredentials;
            proxyInfo.Username = Settings.Default.ProxyUsername;

            if (Settings.Default.ProxyEncryptedPassword != null)
            {
                byte[] password = ProtectedData.Unprotect(
                    Settings.Default.ProxyEncryptedPassword,
                    Encoding.UTF8.GetBytes("ProxyEncryptedPassword"),
                    DataProtectionScope.CurrentUser
                    );
                proxyInfo.Password = Encoding.UTF8.GetString(password);
            }
            return proxyInfo;
        }

        private static bool TryGetSettings(ref int? port,
                                           ProxyInformation proxyInfo)
        {
            var view = new SettingsForm();
            var presenter = new SettingsViewPresenter(view, proxyInfo);
            presenter.Port = port ?? Settings.Default.TfsPort;
            presenter.Show();


            if (!presenter.Cancelled)
            {
            	port = presenter.Port;

            	SaveSettings(proxyInfo, presenter.Port);
            }
        	return !presenter.Cancelled;
        }

    	public static void SaveSettings(ProxyInformation proxyInfo, int port)
    	{
    		Settings.Default.TfsPort = port;
    		Settings.Default.UseProxy = proxyInfo.UseProxy;
    		Settings.Default.ProxyUrl = proxyInfo.Url;
    		Settings.Default.ProxyPort = proxyInfo.Port;
    		Settings.Default.ProxyUseDefaultCredentials = proxyInfo.UseDefaultCredentails;
    		Settings.Default.ProxyUsername = proxyInfo.Username;

    		byte[] password = null;
    		if (proxyInfo.Password != null)
    		{
    			password = ProtectedData.Protect(
    				Encoding.UTF8.GetBytes(proxyInfo.Password),
    				Encoding.UTF8.GetBytes("ProxyEncryptedPassword"),
    				DataProtectionScope.CurrentUser
    				);
    		}
    		Settings.Default.ProxyEncryptedPassword = password;

    		Settings.Default.Save();
    	}

    	private static void Run(int port, ProxyInformation proxyInformation)
        {
            Proxy.Set(proxyInformation);

            var listener = Container.Resolve<Listener>();

            listener.Port = port;

            var view = new ToolTrayForm();
            var presenter = new ListenerViewPresenter(
                view,
                new ErrorsView(),
                listener);

            try
            {
                presenter.Show();
                try
                {
                    presenter.StartListener();
                }
                catch (Exception e)
                {
                    MessageBox.Show(string.Format("Could not start listening: {0}{1}{2}", e.Message, Environment.NewLine,
                                                  e));
                    return;
                }

                Application.Run(view);
            }
            finally
            {
                presenter.StopListener();
            }
        }
    }
}