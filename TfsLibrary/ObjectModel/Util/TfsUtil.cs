using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using CodePlex.TfsLibrary.Utility;

namespace CodePlex.TfsLibrary.ObjectModel
{
    public static class TfsUtil
    {
		//We have to handle it this way because if the app has a restart and immediately tries to create a connection
		// it will pick up the old port automatically, which will obviously be locked
		private static int LastBindPortUsed = (int)(DateTime.Now.Ticks % 60000) + 5001;

        public const int CodePage_Binary = -1;
        public const int CodePage_Unknown = -2;
        static readonly byte[] ByteOrderMark_UTF16_BigEndian = { 0xFE, 0xFF };
        static readonly byte[] ByteOrderMark_UTF16_LittleEndian = { 0xFF, 0xFE };
        static readonly byte[] ByteOrderMark_UTF32_BigEndian = { 0x00, 0x00, 0xFE, 0xFF };
        static readonly byte[] ByteOrderMark_UTF32_LittleEndian = { 0xFF, 0xFE, 0x00, 0x00 };
        static readonly byte[] ByteOrderMark_UTF8 = { 0xEF, 0xBB, 0xBF };
        public static readonly int CodePage_UTF16_BigEndian = 1201;
        public static readonly int CodePage_UTF16_LittleEndian = 1200;
        public static readonly int CodePage_UTF32_BigEndian = 12001;
        public static readonly int CodePage_UTF32_LittleEndian = 12000;
        public static readonly int CodePage_UTF8 = 65001;

        public static int CodePage_ANSI
        {
            get { return (int)GetACP(); }
        }

        public static string CombineProjectPath(string path1,
                                                string path2)
        {
            Guard.ArgumentNotNull(path1, "path1");
            Guard.ArgumentNotNull(path2, "path2");

            string result;

            if (path2.StartsWith("/") || path2.StartsWith("$/")) // Non-relative path 2
                result = path2 + "/";
            else
                result = path1 + "/" + path2 + "/";

            while (result.Contains("//"))
                result = result.Replace("//", "/");

            if (result.StartsWith("$/"))
                return result;
            if (result[0] == '/')
                return "$" + result;
            return "$/" + result;
        }

        static bool CompareBytes(byte[] expected,
                                 byte[] toCompare,
                                 int bytesRead)
        {
            if (bytesRead < expected.Length)
                return false;

            for (int idx = 0; idx < expected.Length; ++idx)
                if (expected[idx] != toCompare[idx])
                    return false;

            return true;
        }

        public static string FailuresToMessage(IEnumerable<Failure> failures)
        {
            Guard.ArgumentNotNull(failures, "failures");

            List<string> messages = new List<string>();
            messages.Add("The following errors occurred:");

            foreach (Failure failure in failures)
                messages.Add(failure.Message);

            return string.Join(Environment.NewLine, messages.ToArray());
        }

        [DllImport("Kernel32.dll")]
        static extern uint GetACP();

        public static int GetStreamCodePage(Stream stream)
        {
            Guard.ArgumentNotNull(stream, "stream");
            Guard.ArgumentValid(stream.CanRead, "Stream must be able to read");
            Guard.ArgumentValid(stream.CanSeek, "Stream must be able to seek");

            try
            {
                // Identification of Unicode via the preamble

                byte[] preamble = new byte[8];
                stream.Seek(0, SeekOrigin.Begin);
                int read = stream.Read(preamble, 0, preamble.Length);

                if (CompareBytes(ByteOrderMark_UTF32_LittleEndian, preamble, read))
                    return CodePage_UTF32_LittleEndian;
                if (CompareBytes(ByteOrderMark_UTF32_BigEndian, preamble, read))
                    return CodePage_UTF32_BigEndian;
                if (CompareBytes(ByteOrderMark_UTF16_LittleEndian, preamble, read))
                    return CodePage_UTF16_LittleEndian;
                if (CompareBytes(ByteOrderMark_UTF16_BigEndian, preamble, read))
                    return CodePage_UTF16_BigEndian;
                if (CompareBytes(ByteOrderMark_UTF8, preamble, read))
                    return CodePage_UTF8;

                // Scan first 64k for a zero byte to see if it's binary

                byte[] buffer = new byte[65536];
                stream.Seek(0, SeekOrigin.Begin);
                read = stream.Read(buffer, 0, buffer.Length);

                for (int idx = 0; idx < read; ++idx)
                    if (buffer[idx] == 0)
                        return CodePage_Binary;

                // TODO: This assumes text files are all in the current application code page

                return CodePage_ANSI;
            }
            catch (IOException)
            {
                return CodePage_Unknown;
            }
        }

        public static string GetUsername(ICredentials credentials,
                                         string url)
        {
            if (credentials == null)
                return WindowsIdentity.GetCurrent().Name;

            NetworkCredential netCredentials = credentials.GetCredential(new Uri(url), "Basic");

            if (netCredentials == null)
                throw new ArgumentException("Only Basic credentials are supported");

            if (string.IsNullOrEmpty(netCredentials.UserName))
                return WindowsIdentity.GetCurrent().Name;

            if (string.IsNullOrEmpty(netCredentials.Domain))
                return netCredentials.UserName;

            return netCredentials.Domain + "\\" + netCredentials.UserName;
        }

        public static string LocalPathToServerPath(string baseServerPath,
                                                   string baseDirectory,
                                                   string localItemPath,
                                                   ItemType itemType)
        {
            string relativePath = FileUtil.GetRelativePath(baseDirectory, localItemPath);

            if (relativePath == localItemPath)
                throw new ArgumentException("Path is not relative", "localItemPath");

            string result = CombineProjectPath(baseServerPath, relativePath);
            return result.Substring(0, result.Length - 1).Replace('\\', '/').TrimEnd('/');
        }

        public static LocalVersionUpdate[] LocalUpdatesToLocalVersionUpdates(IEnumerable<LocalUpdate> updates)
        {
            List<LocalVersionUpdate> result = new List<LocalVersionUpdate>();

            foreach (LocalUpdate update in updates)
                result.Add(LocalUpdateToLocalVersionUpdate(update));

            return result.ToArray();
        }

        public static LocalVersionUpdate LocalUpdateToLocalVersionUpdate(LocalUpdate update)
        {
            return new LocalVersionUpdate(update.ItemId, update.LocalName, update.LocalChangesetID);
        }

        public static void PendRequestsToChangeRequests(IEnumerable<PendRequest> requests,
                                                        out ChangeRequest[] addRequests,
                                                        out ChangeRequest[] editRequests,
                                                        out ChangeRequest[] deleteRequests,
                                                        out ChangeRequest[] copyRequests,
                                                        out ChangeRequest[] renameRequests)
        {
            List<ChangeRequest> adds = new List<ChangeRequest>();
            List<ChangeRequest> edits = new List<ChangeRequest>();
            List<ChangeRequest> deletes = new List<ChangeRequest>();
            List<ChangeRequest> copies = new List<ChangeRequest>();
            List<ChangeRequest> renames = new List<ChangeRequest>();

            foreach (PendRequest request in requests)
                if (request.RequestType == PendRequestType.Add)
                    adds.Add(PendRequestToChangeRequest(request));
                else if (request.RequestType == PendRequestType.Edit)
                    edits.Add(PendRequestToChangeRequest(request));
                else if (request.RequestType == PendRequestType.Delete)
                    deletes.Add(PendRequestToChangeRequest(request));
                else if (request.RequestType == PendRequestType.Copy)
                    copies.Add(PendRequestToChangeRequest(request));
                else if (request.RequestType == PendRequestType.Rename)
                    renames.Add(PendRequestToChangeRequest(request));

            addRequests = adds.ToArray();
            editRequests = edits.ToArray();
            deleteRequests = deletes.ToArray();
            copyRequests = copies.ToArray();
            renameRequests = renames.ToArray();
        }

        public static ChangeRequest PendRequestToChangeRequest(PendRequest pendRequest)
        {
            Guard.ArgumentNotNull(pendRequest, "pendRequest");

            ChangeRequest result = new ChangeRequest();
            result.item = new ItemSpec();
            result.vspec = VersionSpec.Latest;
            switch (pendRequest.RequestType)
            {
                case PendRequestType.Add:
                    result.type = pendRequest.ItemType;
                    result.req = RequestType.Add;
                    result.item.item = pendRequest.LocalName;
                    result.enc = pendRequest.CodePage;
                    break;

                case PendRequestType.Edit:
                    result.req = RequestType.Edit;
                    result.item.item = pendRequest.LocalName;
                    result.@lock = LockLevel.None;
                    break;

                case PendRequestType.Delete:
                    result.req = RequestType.Delete;
                    result.item.item = pendRequest.LocalName;
                    break;

                case PendRequestType.Copy:
                    result.req = RequestType.Branch;
                    result.item.item = pendRequest.LocalName;
                    result.item.recurse = RecursionType.Full;
                    result.target = pendRequest.TargetName;
                    break;

                case PendRequestType.Rename:
                    result.req = RequestType.Rename;
                    result.item.item = pendRequest.LocalName;
                    result.target = pendRequest.TargetName;
                    break;

                default:
                    throw new ArgumentException("Unexpected request type " + pendRequest.RequestType, "pendRequest");
            }

            return result;
        }

        public static string ServerPathToLocalPath(string baseServerPath,
                                                   string baseLocalPath,
                                                   string serverItemPath)
        {
            if (baseServerPath.EndsWith("/"))
                baseServerPath = baseServerPath.Substring(0, baseServerPath.Length - 1);

            string lowerBaseServerPath = baseServerPath.ToLowerInvariant();
            string lowerServerItemPath = serverItemPath.ToLowerInvariant();

            if (!lowerServerItemPath.StartsWith(lowerBaseServerPath))
                throw new ArgumentException(serverItemPath + " not contained inside " + baseServerPath, "serverItemPath");

            if (lowerBaseServerPath == lowerServerItemPath)
                return baseLocalPath;

            string relativeServerPath = serverItemPath.Substring(baseServerPath.Length + 1); // +1 so relative path doesn't start with "/"

            return Path.Combine(baseLocalPath, relativeServerPath.Replace('/', '\\'));
        }

        public static WebRequest SetupWebRequest(WebRequest result)
        {
            return SetupWebRequest(result, CredentialCache.DefaultNetworkCredentials);
        }

        public static Action<WebRequest> OnSetupWebRequest;

        public static WebRequest SetupWebRequest(WebRequest result,
                                                 ICredentials credentials)
        {
            result.Timeout = Timeout.Infinite;

            HttpWebRequest httpResult = result as HttpWebRequest;

            if (httpResult != null)
            {
                httpResult.Credentials = credentials;
                // .ServicePoint property resolving seems very painful
                // (proxy lookup etc.),
                // thus *maybe* caching in local variable helps a bit.
                var servicePoint = httpResult.ServicePoint;
                servicePoint.UseNagleAlgorithm = false;
                httpResult.SendChunked = false;
                httpResult.Pipelined = false;
                httpResult.KeepAlive = true;
                httpResult.PreAuthenticate = false;
                httpResult.UserAgent = "CodePlexClient";

				// these are needed in order to handle large amount of connections
				// to a single server in a short period of time
				servicePoint.BindIPEndPointDelegate = BindIPEndPointCallback;
				httpResult.UnsafeAuthenticatedConnectionSharing = true;
            }
            if (OnSetupWebRequest != null)
                OnSetupWebRequest(httpResult);
            
            return result;
        }

		/// <summary>
		/// This increase the range of local ports we can use for requests
		/// Solve the Only one usage of each socket address (protocol/network address/port) is normally permitted
		/// error under load
		/// See the link for details:
		/// http://blogs.msdn.com/dgorti/archive/2005/09/18/470766.aspx
		/// </summary>
		private static IPEndPoint BindIPEndPointCallback(ServicePoint servicePoint, IPEndPoint remoteEndPoint, int retryCount)
		{
			int port = Interlocked.Increment(ref LastBindPortUsed); //increment
			Interlocked.CompareExchange(ref LastBindPortUsed, 5001, 65534);
			if (remoteEndPoint.AddressFamily == AddressFamily.InterNetwork)
				return new IPEndPoint(IPAddress.Any, port);
			return new IPEndPoint(IPAddress.IPv6Any, port);
		}
    }
}