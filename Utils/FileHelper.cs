using System.Diagnostics;

namespace UNICUT.Utils
{
    public static class FileHelper
    {
        public static void OpenFolderAndSelectFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{filePath}\"",
                UseShellExecute = true
            });
        }
    }
}
