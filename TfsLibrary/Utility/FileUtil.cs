using System.IO;

namespace CodePlex.TfsLibrary.Utility
{
    public static class FileUtil
    {
        public static string GetRelativePath(string basePath,
                                             string path)
        {
            path = Path.GetFullPath(path);

            string lowerPath = path.ToLowerInvariant();
            string lowerBasePath = basePath.ToLowerInvariant();

            // Detect the case where the base path and the path point to the same spot
            if (lowerPath == lowerBasePath)
                return "";

            if (!lowerBasePath.EndsWith("\\"))
                lowerBasePath += "\\";

            if (!lowerPath.StartsWith(lowerBasePath))
                return path;

            return path.Substring(lowerBasePath.Length);
        }
    }
}