using System.IO;

namespace Otokoneko.Server.Utils
{
    public static class DirectoryUtils
    {
        public static void Delete(string path)
        {
            foreach (var file in Directory.GetFiles(path))
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (var dir in Directory.GetDirectories(path))
            {
                Delete(dir);
            }

            Directory.Delete(path, false);
        }
    }
}
