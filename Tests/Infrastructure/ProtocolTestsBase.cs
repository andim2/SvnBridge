using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using CodePlex.TfsLibrary.ObjectModel;
using CodePlex.TfsLibrary.RepositoryWebSvc;
using SvnBridge.Infrastructure;
using SvnBridge.Infrastructure.Statistics;
using SvnBridge.Interfaces;
using Xunit;
using SvnBridge;
using SvnBridge.Net;
using SvnBridge.PathParsing;
using SvnBridge.SourceControl;
using SvnBridge.Utility;

namespace Tests
{
    public abstract class ProtocolTestsBase : IDisposable
    {
        protected HttpContextDispatcher HttpDispatcher;
        protected TFSSourceControlProvider provider;
        protected MyMocks stubs = new MyMocks();

        protected ProtocolTestsBase()
        {
            provider = stubs.CreateTFSSourceControlProviderStub();
            Container.Register(typeof(TFSSourceControlProvider), provider);
            PathParserSingleServerWithProjectInPath pathParser = new PathParserSingleServerWithProjectInPath("http://foo");
            HttpDispatcher = new HttpContextDispatcher(pathParser, stubs.CreateObject<ActionTrackingViaPerfCounter>());
            RequestCache.Init();
        }

        public void Dispose()
        {
            Container.Reset();
        }

        protected static byte[] GetBytes(string data)
        {
            byte[] result = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                result[i] = (byte) data[i];
            }
            return result;
        }

        protected static SourceItemChange MakeChange(ChangeType changeType, string serverPath)
        {
            return TestHelper.MakeChange(changeType, serverPath);
        }

        protected static SourceItemChange MakeChange(ChangeType changeType, string serverPath, string originalPath, int originalRevision)
        {
            return TestHelper.MakeChange(changeType, serverPath, originalPath, originalRevision);
        }

        protected string ProcessRequest(string request, ref string expected)
        {
            MemoryStream HttpStream = new MemoryStream(1024*64);

            byte[] requestBuffer = GetBytes(request);
            HttpStream.Write(requestBuffer, 0, requestBuffer.Length);

            long responseStart = HttpStream.Position;
            HttpStream.Position = 0;

            ListenerContext context = new ListenerContext(HttpStream, stubs.CreateObject<DefaultLogger>());
            HttpDispatcher.Dispatch(context);
            context.Response.Close();

            HttpStream.Position = responseStart;
            byte[] responseBuffer = new byte[Constants.BufferSize];
            int responseLength = HttpStream.Read(responseBuffer, 0, responseBuffer.Length);

            string response = Encoding.UTF8.GetString(responseBuffer, 0, responseLength);

            expected = expected.Replace("Keep-Alive: timeout=15, max=99", "Keep-Alive: timeout=15, max=100");

            bool haveKeepAlive = false;
            if (!(haveKeepAlive))
            {
                expected = expected.Replace("Keep-Alive: timeout=15, max=100\r\n", "");
                expected = expected.Replace("Connection: Keep-Alive", "Connection: close");
            }

            expected = RemoveDate(expected);
            response = RemoveDate(response);

            expected = RemoveChunkedEncoding(expected);
            response = RemoveChunkedEncoding(response);

            return response;
        }

        private static string RemoveDate(string value)
        {
            int startIndex = value.IndexOf("Date:");
            if (startIndex > 0)
            {
                int endIndex = value.IndexOf("\r\n", startIndex);
                return value.Remove(startIndex, endIndex - startIndex + 2);
            }
            else
            {
                return value;
            }
        }

        private static string RemoveChunkedEncoding(string value)
        {
            string result = value;

            int bodyStart = value.IndexOf("\r\n\r\n") + 4;

            Regex regex = new Regex("^[0-9a-f]+\r\n", RegexOptions.Multiline);
            Match match = regex.Match(result, bodyStart);
            while (match.Length > 0)
            {
                result = result.Remove(match.Index, match.Length);
                match = regex.Match(result, bodyStart);
            }

            regex = new Regex("\r\n", RegexOptions.Multiline);
            match = regex.Match(result, bodyStart);
            while (match.Length > 0)
            {
                result = result.Remove(match.Index, match.Length);
                match = regex.Match(result, bodyStart);
            }

            return result;
        }

        protected T DeserializeRequest<T>(string xml)
        {
            return Helper.DeserializeXml<T>(xml);
        }

        protected string SerializeResponse<T>(T response, XmlSerializerNamespaces ns)
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.CloseOutput = false;
            settings.Encoding = Encoding.UTF8;
            StringBuilder xml = new StringBuilder();
            XmlWriter writer = XmlWriter.Create(xml, settings);
            XmlSerializer serializer = new XmlSerializer(typeof (T));
            serializer.Serialize(writer, response, ns);
            writer.Flush();
            return xml.ToString();
        }

        protected ItemMetaData CreateItem(string name)
        {
            ItemMetaData item = new ItemMetaData();
            item.Name = name;
            return item;
        }

        protected FolderMetaData CreateFolder(string name)
        {
            FolderMetaData folder = new FolderMetaData();
            folder.Name = name;
            return folder;
        }

        protected FolderMetaData CreateFolder(string name, int revision)
        {
            FolderMetaData folder = new FolderMetaData();
            folder.Name = name;
            folder.ItemRevision = revision;
            return folder;
        }

        protected FolderMetaData CreateFolder(string name, int revision, string lastModifiedDate)
        {
            FolderMetaData folder = new FolderMetaData();
            folder.Name = name;
            folder.ItemRevision = revision;
            folder.LastModifiedDate = DateTime.Parse(lastModifiedDate);
            return folder;
        }
    }
}
