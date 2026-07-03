using System.Drawing;

namespace UNICUT.Core
{
    public class CaptureService
    {
        public Bitmap CaptureRegion(Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return null;

            Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            }

            return bitmap;
        }
    }
}
