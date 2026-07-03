using System;
using System.Drawing;
using System.IO;

namespace UNICUT.Core
{
    public class SaveService
    {
        public string SaveBitmap(Bitmap bitmap)
        {
            try
            {
                string picturesFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                string unicutFolder = Path.Combine(picturesFolder, "UNICUT");

                if (!Directory.Exists(unicutFolder))
                {
                    Directory.CreateDirectory(unicutFolder);
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"unicut_{timestamp}.png";
                string filePath = Path.Combine(unicutFolder, fileName);

                bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);

                return filePath;
            }
            catch (Exception ex)
            {
                // In a real app we'd log this or show a message.
                System.Diagnostics.Debug.WriteLine("Failed to save image: " + ex.Message);
                return null;
            }
        }
    }
}
