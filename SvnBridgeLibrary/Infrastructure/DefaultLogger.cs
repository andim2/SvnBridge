using System;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;
using SvnBridge.Interfaces;
using SvnBridge.Net;
using SvnBridge.Utility;

namespace SvnBridge.Infrastructure
{
    public class DefaultLogger
    {
        private string logPath;

        private string LogPath
        {
            get
            {
                if (logPath != null)
                    return logPath;

                logPath = Configuration.LogPath;
                if (logPath != null)
                    return logPath;

                logPath = "";
                try
                {
                    try
                    {
                        File.WriteAllText("tmp.log", "test");
                        File.Delete("tmp.log");
                    }
                    catch (UnauthorizedAccessException)
                    {
                        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        logPath = Path.Combine(localAppData, "SvnBridge");
                        if (Directory.Exists(logPath) == false)
                            Directory.CreateDirectory(logPath);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    throw new UnauthorizedAccessException(
                        string.Format(
                            "Tried to write to a log file in: {0} and in {1}, but did not have the required permissions to do so." +
                            Environment.NewLine +
                            "Please set the permissions for either of those locations, or set the 'LogPath' property in the application configuration file.",
                            Environment.CurrentDirectory, Path.GetFullPath(logPath)));
                }
                return logPath;
            }
        }

        public virtual void Error(string message, Exception exception)
        {
            var we = exception as WebException;
            if (we != null && we.Response != null)
            {
                var hwr = we.Response as HttpWebResponse;
                if (hwr != null && hwr.StatusCode != HttpStatusCode.Unauthorized)
                {
                    using (var sr = new StreamReader(we.Response.GetResponseStream()))
                    {
                        var sb = new StringBuilder(message);
                        sb.AppendLine(" Error page is:");
                        sb.AppendLine(sr.ReadToEnd());
                        message = sb.ToString();
                    }
                }
            }
            Log("Error", message, exception.ToString());
        }

        public virtual void ErrorFullDetails(Exception exception, IHttpContext context)
        {           
            string host = context.Request.Headers["Host"];
            if (!string.IsNullOrEmpty(host))
                host = host.Replace('.', '-').Replace(':', '-') + "-";

            Guid errorId = Guid.NewGuid();
            string logFile = Path.Combine(LogPath, string.Format("{0}Error-{1}.log", !string.IsNullOrEmpty(host) ? host : "", errorId));
            var output = new StringBuilder();
            output.AppendFormat("Time     : {0}\r\n", DateTime.Now);
            output.AppendFormat("Message  : {0}\r\n", exception.Message);
            var credential = (NetworkCredential) RequestCache.Items["credentials"];
            if (credential != null)
            {
                output.AppendFormat("User     : {0}\r\n", credential.UserName);
            }
            output.AppendFormat("Request  : {0} {1} HTTP/1.1\r\n", context.Request.HttpMethod, context.Request.Url.AbsolutePath);
            if (RequestCache.Items["RequestBody"] != null)
            {
                output.AppendFormat("{0}\r\n", Helper.SerializeXmlString(RequestCache.Items["RequestBody"]));
            }
            output.AppendFormat("\r\nException:\r\n   {0}\r\n", exception);
            output.AppendFormat("\r\nStack Trace:\r\n{0}\r\n", exception.StackTrace);
            output.Append("\r\nHeaders:\r\n\r\n");
            foreach (string name in context.Request.Headers)
            {
                foreach (string value in context.Request.Headers[name].Split(','))
                {
                    output.Append(name);
                    output.Append(": ");
                    output.Append(value.TrimStart());
                    output.Append("\r\n");
                }
            }

            File.WriteAllText(logFile, output.ToString());
        }

        public virtual void Info(string message, Exception exception)
        {
            Log("Info", message, exception.ToString());
        }

        public virtual void Trace(string message, params object[] args)
        {
            if (Logging.TraceEnabled == false)
                return;
            Log("Trace", string.Format(message, args), null);
        }

        public virtual void TraceMessage(string message)
        {
            if (Logging.TraceEnabled == false)
                return;
            Log("TraceMessage", message, null);
        }

        private void Log(string level, string message, string exception)
        {
            try
            {
                WriteLogMessageWithNoExceptionHandling(level, message, exception);
            }
            catch (Exception)
            {
                //We don't have anything to do here, can't fix errors in error handling code
            }
        }

        private void WriteLogMessageWithNoExceptionHandling(string level, string message, string exception)
        {
            var settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.OmitXmlDeclaration = true;

            string logFile = Path.Combine(LogPath, level + ".log");

            using (StreamWriter text = File.AppendText(logFile))
            using (XmlWriter writer = XmlWriter.Create(text, settings))
            {
                writer.WriteStartElement("log");
                writer.WriteAttributeString("level", level);
                WriteCDataElement(writer, "message", message);
                if (string.IsNullOrEmpty(exception) == false)
                    WriteCDataElement(writer, "exception", exception);
                writer.WriteEndElement();
            }
        }

        private static void WriteCDataElement(XmlWriter writer, string name, string message)
        {
            writer.WriteStartElement(name);
            writer.WriteCData(message);
            writer.WriteEndElement();
        }
    }
}
