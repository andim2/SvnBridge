using System.IO;
using System.Net;
using SvnBridge.Interfaces;
using SvnBridge.Net;
using SvnBridge.SourceControl;

namespace SvnBridge.Handlers.Renderers
{
    internal class FolderRenderer
    {
        private readonly IHttpContext context;
        private readonly IPathParser pathParser;
        private readonly ICredentials credentials;
        private readonly StreamWriter writer;
        private readonly string applicationPath;

        public FolderRenderer(IHttpContext context, IPathParser pathParser, ICredentials credentials)
        {
            this.context = context;
            this.pathParser = pathParser;
            this.credentials = credentials;
            applicationPath = pathParser.GetApplicationPath(context.Request);
            if (applicationPath.EndsWith("/"))
                applicationPath = applicationPath.Substring(0, applicationPath.Length - 1);
            if (applicationPath.StartsWith("/") == false)
                applicationPath = "/" + applicationPath;
            writer = new StreamWriter(context.Response.OutputStream);
        }

        public void Render(FolderMetaData folder)
        {
            writer.WriteLine("<html>");
            writer.Write("<title>");
            writer.Write(GetFolderName(folder));
            writer.WriteLine("</title>");
            writer.Write("<body>");
            writer.Write("<h1>Contents of ");
            writer.Write(GetFolderName(folder));
            writer.WriteLine("</h1>");
            writer.Write("<ul>");
            writer.Write("<li><a href='..'>..</a></li>");
            foreach (ItemMetaData item in folder.Items)
            {
                writer.Write("<li><a href='");
                writer.Write(applicationPath);
                writer.Write(item.Name);
                writer.WriteLine("'>");
                writer.Write(item.Name);
                writer.WriteLine("</a></li>");
            }
            writer.WriteLine("</ul>");

            writer.Write("</body>");
            writer.WriteLine("</html>");
            writer.Flush();
        }

        private string GetFolderName(ItemMetaData folder)
        {
            string projectName = pathParser.GetProjectName(context.Request);
            if (projectName != null)
                return "Project " + projectName + " " + folder.Name;
            return folder.Name + " @ " + pathParser.GetServerUrl(context.Request, credentials);
        }
    }
}