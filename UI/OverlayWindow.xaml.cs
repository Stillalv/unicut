using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using UNICUT.Core;

namespace UNICUT.UI
{
    public partial class OverlayWindow : Window
    {
        private System.Windows.Point _startPoint;
        private bool _isDrawing;

        public OverlayWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Cover all screens
            this.Left = SystemParameters.VirtualScreenLeft;
            this.Top = SystemParameters.VirtualScreenTop;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _startPoint = e.GetPosition(OverlayCanvas);
                _isDrawing = true;
                SelectionRectangle.Visibility = Visibility.Visible;
                Canvas.SetLeft(SelectionRectangle, _startPoint.X);
                Canvas.SetTop(SelectionRectangle, _startPoint.Y);
                SelectionRectangle.Width = 0;
                SelectionRectangle.Height = 0;
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDrawing)
            {
                var pos = e.GetPosition(OverlayCanvas);
                
                var x = Math.Min(pos.X, _startPoint.X);
                var y = Math.Min(pos.Y, _startPoint.Y);
                
                var w = Math.Max(pos.X, _startPoint.X) - x;
                var h = Math.Max(pos.Y, _startPoint.Y) - y;

                Canvas.SetLeft(SelectionRectangle, x);
                Canvas.SetTop(SelectionRectangle, y);
                SelectionRectangle.Width = w;
                SelectionRectangle.Height = h;
            }
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDrawing)
            {
                _isDrawing = false;
                
                // Get bounds
                int x = (int)Canvas.GetLeft(SelectionRectangle) + (int)this.Left;
                int y = (int)Canvas.GetTop(SelectionRectangle) + (int)this.Top;
                int w = (int)SelectionRectangle.Width;
                int h = (int)SelectionRectangle.Height;

                if (w > 0 && h > 0)
                {
                    PerformCapture(new System.Drawing.Rectangle(x, y, w, h));
                }
                else
                {
                    this.Close();
                }
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
        }

        private async void PerformCapture(System.Drawing.Rectangle bounds)
        {
            this.Hide();
            // Wait slightly for window to disappear
            await System.Threading.Tasks.Task.Delay(100);

            var captureService = new CaptureService();
            var bitmap = captureService.CaptureRegion(bounds);

            if (bitmap != null)
            {
                var saveService = new SaveService();
                string filePath = saveService.SaveBitmap(bitmap);

                if (!string.IsNullOrEmpty(filePath))
                {
                    // Copy to clipboard
                    Utils.ClipboardHelper.CopyImage(filePath);

                    // Show Editor Popup
                    var editorPopup = new EditorPopup(filePath, bitmap);
                    editorPopup.Show();
                }
            }

            this.Close();
        }
    }
}
