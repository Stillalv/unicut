using System;
using System.Drawing;

namespace UNICUT.Models
{
    public class ScreenshotModel
    {
        public Bitmap Image { get; set; }
        public string FilePath { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
