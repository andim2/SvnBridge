using System.Reflection;

namespace CodePlex.TfsLibrary.Utility
{
    public static class ReflectionUtil
    {
        public static string GetAssemblyFilename(Assembly assembly)
        {
            return assembly.CodeBase.Replace("file:///", "").Replace('/', '\\');
        }

        public static string GetAssemblyVersion(Assembly assembly)
        {
            return assembly.GetName().Version.ToString();
        }
    }
}