namespace SvnBridge.SourceControl
{
    public class ProjectLocationInformation
    {
        public string RemoteProjectName;
        public string ServerUrl;

        public ProjectLocationInformation(string remoteProjectName, string serverUrl)
        {
            RemoteProjectName = remoteProjectName;
            ServerUrl = serverUrl;
        }
    }
}