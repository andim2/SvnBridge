using System;
using System.IO;
using System.Net;
using System.Xml;
using SvnBridge.SourceControl;

namespace SvnBridge.Infrastructure
{
    /// <summary>
    /// This implementation is probably not the best, but we had two problems with it.
    /// First, we can't take dependencies on the TFS API, we would need to redistribute it with us, and 
    /// that is problematic. The second is that the ClientService API is complex and undocumented, which 
    /// means that it is actually easier to use this approach than through the SOAP proxy.
    /// </summary>
    /// <remarks>
    /// Yes, we shouldn't have to write our own SOAP handling, sorry about that.
    /// </remarks>
    public class TfsWorkItemModifier : IWorkItemModifier
    {
        private readonly static string associateWorkItemWithChangeSetMessage;
        private readonly static string getWorkItemInformationMessage;
        private static readonly string setWorkItemStatusToFixedMessage;

        private readonly string serverUrl;
        private readonly ICredentials credentials;
        private readonly string username;


        /// <summary>
        /// Preferred ctor variant (explicitly supplies a username identifier
        /// as possibly gathered via precise server session information
        /// rather than trying to dirtily infer it from credentials object).
        /// All params (especially "sessionUsername") may be gathered *implicitly*
        /// via construction-time Container resolving.
        /// For detailed comments about username requirement,
        /// see setup side.
        /// </summary>
        public TfsWorkItemModifier(string serverUrl, ICredentials credentials, string sessionUserName)
        {
            this.serverUrl = serverUrl;
            this.credentials = CredentialsHelper.GetCredentialsForServer(serverUrl, credentials);
            this.username = sessionUserName;
        }

        /// <summary>
        /// Deprecated ctor variant (does not explicitly supply a username identifier -
        /// thus need to resort to dirtily-grab-username-from-credentials fallback,
        /// which will be impossible with properly secure non-plaintext-type credentials).
        /// </summary>
        public TfsWorkItemModifier(string serverUrl, ICredentials credentials)
        {
            this.serverUrl = serverUrl;
            this.credentials = CredentialsHelper.GetCredentialsForServer(serverUrl, credentials);
            // HACK: when no auth credentials were provided, we obviously might end up with null credentials,
            // so avoid gathering username from there (this object currently gets implicitly container-constructed,
            // so we are currently forced to not having its construction fail,
            // despite it then most likely not being usable).
            this.username = TryGrabUsername();
        }

        private string TryGrabUsername()
        {
            //string unknownuser = "anonymous";
            string unknownuser = "CouldNotDetermineUser";
            return (null != this.credentials) ? this.credentials.GetCredential(new Uri(serverUrl), "basic").UserName : unknownuser;
        }

        static TfsWorkItemModifier()
        {
            using (Stream stream = typeof(TfsWorkItemModifier).Assembly.GetManifestResourceStream(
                "SvnBridge.Infrastructure.Messages.AssociateWorkItemWithChangeSetMessage.xml"))
            {
                associateWorkItemWithChangeSetMessage = new StreamReader(stream).ReadToEnd();
            }

            using (Stream stream = typeof(TfsWorkItemModifier).Assembly.GetManifestResourceStream(
                "SvnBridge.Infrastructure.Messages.GetWorkItemInformationMessage.xml"))
            {
                getWorkItemInformationMessage = new StreamReader(stream).ReadToEnd();
            }

            using (Stream stream = typeof(TfsWorkItemModifier).Assembly.GetManifestResourceStream(
                "SvnBridge.Infrastructure.Messages.SetWorkItemStatusToFixedMessage.xml"))
            {
                setWorkItemStatusToFixedMessage = new StreamReader(stream).ReadToEnd();
            }
        }

        public virtual void Associate(int workItemId, int changeSetId)
        {

            HttpWebRequest request = GetWebRequest();
            request.ContentType =
                "application/soap+xml; charset=utf-8; action=\"http://schemas.microsoft.com/TeamFoundation/2005/06/WorkItemTracking/ClientServices/03/Update\"";

            request.Method = "POST";
            using (Stream stream = request.GetRequestStream())
            {
                using (StreamWriter sw = new StreamWriter(stream))
                {
                    int workItemRevisionId = GetWorkItemInformation(workItemId).Revision;
                    string text =
                        GetAssociateWorkItemWithChangeSetMessage()
                            .Replace("{Guid}", Guid.NewGuid().ToString())
                            .Replace("{WorkItemId}", workItemId.ToString())
                            .Replace("{ChangeSetId}", changeSetId.ToString())
                            .Replace("{RevisionId}", workItemRevisionId.ToString())
                            .Replace("{ServerUrl}", serverUrl)
                            .Replace("{UserName}", username);

                    sw.Write(text);
                }
            }
            try
            {
                // we don't care about the response from here
                request.GetResponse().Close();
            }
            catch (WebException we)
            {
                using (Stream stream = we.Response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    throw new InvalidOperationException("Failed to associate work item " + workItemId + " with changeset " + changeSetId + Environment.NewLine + reader.ReadToEnd(), we);
                }
            }
        }

        public virtual void SetWorkItemFixed(int workItemId, int changeSetId)
        {

            HttpWebRequest request = GetWebRequest();
            request.ContentType =
                "application/soap+xml; charset=utf-8; action=\"http://schemas.microsoft.com/TeamFoundation/2005/06/WorkItemTracking/ClientServices/03/Update\"";

            request.Method = "POST";
            using (Stream stream = request.GetRequestStream())
            {
                using (StreamWriter sw = new StreamWriter(stream))
                {
                    WorkItemInformation information = GetWorkItemInformation(workItemId);
                    if(information.State == "Fixed")
                        return; // already fixed
                    int workItemRevisionId = information.Revision;
                    string text =
                        GetSetWorkItemStatusToFixedMessage()
                            .Replace("{Guid}", Guid.NewGuid().ToString())
                            .Replace("{WorkItemId}", workItemId.ToString())
                            .Replace("{RevisionId}", workItemRevisionId.ToString())
                            .Replace("{ServerUrl}", serverUrl)
                            .Replace("{UserName}", username);

                    sw.Write(text);
                }
            }
            try
            {
                // we don't care about the response from here
                request.GetResponse().Close();
            }
            catch (WebException we)
            {
                using (Stream stream = we.Response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    throw new InvalidOperationException("Failed to set work item " + workItemId + " status to fixed" + Environment.NewLine + reader.ReadToEnd(), we);
                }
            }
        }

        private static string GetSetWorkItemStatusToFixedMessage()
        {
            string custom = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SetWorkItemStatusToFixedMessage.xml");
            if (File.Exists(custom))
                return File.ReadAllText(custom);
            return setWorkItemStatusToFixedMessage;
        }

        private static string GetAssociateWorkItemWithChangeSetMessage()
        {
            string custom = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AssociateWorkItemWithChangeSetMessage.xml");
            if (File.Exists(custom))
                return File.ReadAllText(custom);
            return associateWorkItemWithChangeSetMessage;
        }

        private WorkItemInformation GetWorkItemInformation(int workItemId)
        {
            HttpWebRequest request = GetWebRequest();
            request.ContentType =
                "application/soap+xml; charset=utf-8; action=\"http://schemas.microsoft.com/TeamFoundation/2005/06/WorkItemTracking/ClientServices/03/GetWorkItem\"";

            request.Method = "POST";
            using (Stream stream = request.GetRequestStream())
            {
                using (StreamWriter sw = new StreamWriter(stream))
                {
                    string text = getWorkItemInformationMessage
                        .Replace("{Guid}", Guid.NewGuid().ToString())
                        .Replace("{WorkItemId}", workItemId.ToString());

                    sw.Write(text);
                }
            }

            WebResponse response = request.GetResponse();
            using (StreamReader sr = new StreamReader(response.GetResponseStream()))
            {
                XmlDocument xdoc = new XmlDocument();
                xdoc.Load(sr);
                XmlNamespaceManager nsMgr = new XmlNamespaceManager(xdoc.NameTable);
                nsMgr.AddNamespace("wi", "http://schemas.microsoft.com/TeamFoundation/2005/06/WorkItemTracking/ClientServices/03");
                XmlNode node = xdoc.SelectSingleNode("//wi:GetWorkItemResponse/wi:workItem/wi:table[@name='WorkItemInfo']", nsMgr);
                int indexOfRevision = GetIndexOfColumn(nsMgr, node, "System.Rev");
                int indexOfState = GetIndexOfColumn(nsMgr, node, "System.State");
                XmlNodeList rowNodes = node.SelectNodes("wi:rows/wi:r/wi:f", nsMgr);
                int revisionId = int.Parse(rowNodes[indexOfRevision].InnerText);
                string state = rowNodes[indexOfState].InnerText;
                return new WorkItemInformation(state, revisionId);
            }
        }

        public class WorkItemInformation
        {
            public WorkItemInformation(string state, int revision)
            {
                State = state;
                Revision = revision;
            }

            public string State;
            public int Revision;
        }

        private static int GetIndexOfColumn(XmlNamespaceManager nsMgr, XmlNode node, string columnName)
        {
            int index = 0;
            foreach (XmlNode xmlNode in node.SelectNodes("wi:columns/wi:c/wi:n", nsMgr))
            {
                if (xmlNode.InnerText == columnName)
                    break;
                index += 1;
            }
            return index;
        }

        private HttpWebRequest GetWebRequest()
        {
            HttpWebRequest request =
                (HttpWebRequest)
                WebRequest.Create(serverUrl + "/WorkItemTracking/v1.0/ClientService.asmx");
            request.Credentials = credentials;
            request.UserAgent = "Team Foundation (SvnBridge)";
            request.Headers.Add("X-TFS-Version", "1.0.0.0");
            return request;
        }
    }
}
