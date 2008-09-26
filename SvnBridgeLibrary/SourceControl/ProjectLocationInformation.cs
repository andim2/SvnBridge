namespace SvnBridge.SourceControl
{
    public class ProjectLocationInformation
    {
        public string RemoteProjectName;
        public string ServerUrl;

        public ProjectLocationInformation(string canonizedProjectName, string serverUrl)
        {
            RemoteProjectName = canonizedProjectName;
            ServerUrl = serverUrl;
        }
    }
}