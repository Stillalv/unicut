using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace UNICUT.Utils
{
    public static class ClipboardHelper
    {
        public static void CopyPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            string quotedPath = "\"" + filePath.Trim('\"') + "\"";
            CopyText(quotedPath);
        }

        public static void CopyText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    Clipboard.SetText(text);
                    return;
                }
                catch
                {
                    System.Threading.Thread.Sleep(50);
                }
            }
        }

        public static void CopyImage(string filePath)
        {
            CopyPath(filePath);
        }
    }
}
