using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace UNICUT.Utils
{
    public static class ClipboardHelper
    {
        public static void CopyText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                Clipboard.SetText(text);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Failed to copy text to clipboard: " + ex.Message);
            }
        }

        public static void CopyImage(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            try
            {
                var bitmap = new BitmapImage(new Uri(filePath, UriKind.Absolute));
                Clipboard.SetImage(bitmap);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Failed to copy image to clipboard: " + ex.Message);
            }
        }
    }
}
