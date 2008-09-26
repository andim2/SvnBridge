using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
using CodePlex.TfsLibrary.ObjectModel;
using SvnBridge.Net;

namespace SvnBridge.Utility
{
	public static class Helper
	{
		private static readonly string[] DECODED = new string[] { "%", "#", " ", "^", "{", "[", "}", "]", ";", "`", "&" };
		private static readonly string[] DECODED_B = new string[] { "&", "<", ">" };
		private static readonly string[] DECODED_C = new string[] { "%", "#", " ", "^", "{", "[", "}", "]", ";", "`" };

		private static readonly string[] ENCODED = new string[] { "%25", "%23", "%20", "%5e", "%7b", "%5b", "%7d", "%5d", "%3b", "%60", "&amp;" };
		private static readonly string[] ENCODED_B = new string[] { "&amp;", "&lt;", "&gt;" };
		private static readonly string[] ENCODED_C = new string[] { "%25", "%23", "%20", "%5e", "%7b", "%5b", "%7d", "%5d", "%3b", "%60" };

		public static XmlReaderSettings InitializeNewXmlReaderSettings()
		{
			XmlReaderSettings readerSettings = new XmlReaderSettings();
			readerSettings.CloseInput = false;
			return readerSettings;
		}

		public static T DeserializeXml<T>(XmlReader reader)
		{
			XmlSerializer requestSerializer = new XmlSerializer(typeof(T));
			return (T)requestSerializer.Deserialize(reader);
		}

		public static T DeserializeXml<T>(string xml)
		{
			XmlReader reader = XmlReader.Create(new StringReader(xml), InitializeNewXmlReaderSettings());
			return (T)DeserializeXml<T>(reader);
		}

		public static T DeserializeXml<T>(byte[] xml)
		{
			XmlReader reader = XmlReader.Create(new MemoryStream(xml), InitializeNewXmlReaderSettings());
			return (T)DeserializeXml<T>(reader);
		}

		public static T DeserializeXml<T>(Stream requestStream)
		{
			using (XmlReader reader = XmlReader.Create(requestStream, InitializeNewXmlReaderSettings()))
			{
				return DeserializeXml<T>(reader);
			}
		}

		public static bool IsValidPort(string port)
		{
			int portAsInt;

			if (!int.TryParse(port, out portAsInt))
			{
				return false;
			}

			return IsValidPort(portAsInt);
		}

		public static bool IsValidPort(int port)
		{
			return (port >= 1 && port <= 65535);
		}

		public static bool IsPortInUseOnLocalHost(int port)
		{
			bool inUse = false;
			TcpListener listener = new TcpListener(IPAddress.Loopback, port);
			try
			{
				listener.Start();
			}
			catch (SocketException)
			{
				inUse = true;
			}
			listener.Stop();
			return inUse;
		}

		public static bool IsValidTFSUrl(string url, ProxyInformation proxyInformation)
		{
			try
			{
				WebRequest request = WebRequest.Create(url + "/Services/v1.0/Registration.asmx");
				request.Credentials = CredentialCache.DefaultNetworkCredentials;
				request.Proxy = CreateProxy(proxyInformation);
				request.Timeout = 20000;

				using (WebResponse response = request.GetResponse())
				{
					StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
					string output = reader.ReadToEnd();
					return (output.Contains("Team Foundation Registration web service"));
				}
			}
			catch (WebException e)
			{
				HttpWebResponse response = e.Response as HttpWebResponse;

				if (response != null && response.StatusCode == HttpStatusCode.Unauthorized)
				{
					// we need to ensure that common case of:
					// http://server:80   <- share point
					// htpp://server:8080 <- TFS
					return response.Headers["MicrosoftSharePointTeamServices"] == null;
				}

				return false;
			}
			catch
			{
				return false;
			}
		}

		public static bool IsValidUrl(string url)
		{
			try
			{
				new Uri(url);
				return true;
			}
			catch (UriFormatException)
			{
				return false;
			}
		}

		public static byte[] SerializeXml<T>(T request)
		{
			XmlWriterSettings settings = new XmlWriterSettings();
			settings.CloseOutput = false;
			settings.Encoding = Encoding.UTF8;
			MemoryStream xml = new MemoryStream();
			XmlWriter writer = XmlWriter.Create(xml, settings);
			XmlSerializer serializer = new XmlSerializer(typeof(T));
			XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
			serializer.Serialize(writer, request, ns);
			writer.Flush();
			return xml.ToArray();
		}

		public static string SerializeXmlString(object request)
		{
			StringWriter sw = new StringWriter();
			XmlSerializer serializer = new XmlSerializer(request.GetType());
			XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
			serializer.Serialize(sw, request, ns);
			return sw.GetStringBuilder().ToString();
		}


		public static string GetMd5Checksum(byte[] data)
		{
			MD5 md5 = MD5.Create();
			StringBuilder sb = new StringBuilder();

			foreach (byte b in md5.ComputeHash(data))
			{
				sb.Append(b.ToString("x2").ToLower());
			}

			return sb.ToString();
		}

		private static string Encode(string[] encoded, string[] decoded, string value, bool capitalize)
		{
			if (value == null)
			{
				return value;
			}

			for (int i = 0; i < decoded.Length; i++)
			{
				if (capitalize && decoded[i] != "&")
				{
					value = value.Replace(decoded[i], encoded[i].ToUpper());
				}
				else
				{
					value = value.Replace(decoded[i], encoded[i]);
				}
			}

			return value;
		}

		private static string Decode(string value, bool capitalize)
		{
			if (value == null)
			{
				return value;
			}

			for (int i = ENCODED.Length - 1; i >= 0; i--)
			{
				if (capitalize)
				{
					value = value.Replace(ENCODED[i].ToUpper(), DECODED[i]);
				}
				else
				{
					value = value.Replace(ENCODED[i], DECODED[i]);
				}
			}

			return value;
		}

		public static string Encode(string value)
		{
			return Encode(value, false);
		}

		public static string Encode(string value,
									bool capitalize)
		{
			return Encode(ENCODED, DECODED, value, capitalize);
		}

		public static string Decode(string value)
		{
			return Decode(value, false);
		}

		public static string EncodeB(string value)
		{
			return Encode(ENCODED_B, DECODED_B, value, false);
		}

		public static string DecodeB(string value)
		{
			return Decode(value, false);
		}

		public static string EncodeC(string value)
		{
			return Encode(ENCODED_C, DECODED_C, value, true);
		}

		public static string DecodeC(string value)
		{
			return Decode(value, true);
		}

		public static string FormatDate(DateTime date)
		{
			string result = date.ToUniversalTime().ToString("o");
			return result.Remove(result.Length - 2, 1);
		}

        public static string FormatDateB(DateTime date)
        {
            return date.ToUniversalTime().ToString("R");
        }

		public static IWebProxy CreateProxy(ProxyInformation proxyInformation)
		{
			if (proxyInformation.UseProxy == false)
				return null;
			IWebProxy proxy = new WebProxy(proxyInformation.Url, proxyInformation.Port);
			ICredentials credential;
			if (proxyInformation.UseDefaultCredentails)
			{
				credential = CredentialCache.DefaultNetworkCredentials;
			}
			else
			{
				credential = new NetworkCredential(proxyInformation.Username, proxyInformation.Password);
			}
			proxy.Credentials = credential;
			return proxy;
		}

		public static IList<SourceItemHistory> SortHistories(bool updatingForwardInTime,
														 IEnumerable<SourceItemHistory> items)
		{
			List<SourceItemHistory> histories = new List<SourceItemHistory>(items);

			histories.Sort(delegate(SourceItemHistory x, SourceItemHistory y)
			{
				if (updatingForwardInTime)
				{
					return x.ChangeSetID.CompareTo(y.ChangeSetID);
				}
				else
				{
					return y.ChangeSetID.CompareTo(x.ChangeSetID);
				}
			});
			return histories;
		}

		public static string GetFolderNameUsingServerRootPath(string name)
		{
			int indexOfSlash = name.LastIndexOf("/");
			string folderName = indexOfSlash == -1 ? Constants.ServerRootPath : name.Substring(0, indexOfSlash);
			if (folderName == "$")
				return Constants.ServerRootPath;
			return folderName;
		}

		public static string UrlEncodeIfNeccesary(string href)
		{
			StringBuilder sb = new StringBuilder();
			foreach (char c in href)
			{
				if (c > 256)
				{
					sb.Append(HttpUtility.UrlEncode(c.ToString()));
				}
				else
				{
					sb.Append(c);
				}
			}
			return sb.ToString();
		}

		public static string CombinePath(string path1, string path2)
		{
			if (path1.EndsWith("/"))
			{
				if (path2.StartsWith("/"))
				{
					return path1 + path2.Substring(1);
				}
				return path1 + path2;
			}
			if (path2.StartsWith("/"))
			{
				return path1 + path2;
			}
			return path1 + "/" + path2;
		}
	}
}
