using System.Diagnostics;
using System.IO;

namespace Otokoneko.Client.WPFClient.Utils
{
    public static class FileUtils
    {
        public static void OpenFolder(string folderPath)
        {
            folderPath = Path.GetFullPath(folderPath);
            if (Directory.Exists(folderPath))
            {
                ProcessStartInfo startInfo = new()
                {
                    Arguments = folderPath,
                    FileName = "explorer.exe"
                };

                Process.Start(startInfo);
            }
        }
    }
}
