using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using SvnBridge.Handlers;

namespace Tests
{
    public class TestableOutputStream : MemoryStream
    {
        public override void Close() {}
    }

    public class MockContext : IHttpRequest
    {
        string _httpMethod = null;
        string _path = null;
        NetworkCredential _credentials = null;
        Stream _inputStream = null;
        NameValueCollection _headers = new NameValueCollection();
        NameValueCollection _responseHeaders = new NameValueCollection();
        string _contentType = null;
        Encoding _encoding = null;
        int _statusCode;
        Stream _outputStream = new TestableOutputStream();
        bool _sendChunked = false;

        public string HttpMethod
        {
            get { return _httpMethod; }
            set { _httpMethod = value; }
        }

        public string Path
        {
            get { return _path; }
            set { _path = value; }
        }

        public Stream InputStream
        {
            get { return _inputStream; }
            set { _inputStream = value; }
        }

        public NameValueCollection Headers
        {
            get { return _headers; }
        }

        public NameValueCollection ResponseHeaders
        {
            get { return _responseHeaders; }
        }

        public int StatusCode
        {
            get { return _statusCode; }
            set { _statusCode = value; }
        }

        public string ContentType
        {
            get { return _contentType; }
            set { _contentType = value; }
        }

        public void Write(string output)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(output);
            OutputStream.Write(buffer, 0, buffer.Length);
        }

        public Stream OutputStream
        {
            get { return _outputStream; }
        }

        public void AddHeader(string name,
                              string value)
        {
            _responseHeaders[name] = value;
        }

        public Encoding ContentEncoding
        {
            set { _encoding = value; }
            get { return _encoding; }
        }

        public NetworkCredential Credentials
        {
            get
            {
                string auth = Headers["Authorization"];
                if (auth != null)
                {
                    auth = auth.Substring(auth.IndexOf(' ') + 1);
                    auth = UTF8Encoding.UTF8.GetString(Convert.FromBase64String(auth));
                    string username = auth.Split(':')[0];
                    string password = auth.Split(':')[1];
                    if (username.IndexOf('\\') >= 0)
                    {
                        string domain = username.Substring(0, username.IndexOf('\\'));
                        username = username.Substring(username.IndexOf('\\') + 1);
                        return new NetworkCredential(username, password, domain);
                    }
                    else
                    {
                        return new NetworkCredential(username, password);
                    }
                }
                else
                {
                    return null;
                }
            }
            set { _credentials = value; }
        }

        public bool SendChunked
        {
            set { _sendChunked = value; }
            get { return _sendChunked; }
        }

        public void RemoveHeader(string name)
        {
            throw new Exception("The method or operation is not implemented.");
        }
    }
}