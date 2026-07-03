using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace UNICUT.UI
{
    public partial class EditorPopup : Window
    {
        private string _filePath;
        private System.Drawing.Bitmap _bitmap;
        
        private Point _startPoint;
        private Rectangle _currentRect;
        private bool _isDrawingShape;

        public EditorPopup(string filePath, System.Drawing.Bitmap bitmap)
        {
            InitializeComponent();
            _filePath = filePath;
            _bitmap = bitmap;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_filePath))
            {
                var bi = new BitmapImage(new Uri(_filePath, UriKind.Absolute));
                ImgPreview.Source = bi;
                ImgPreview.Width = bi.PixelWidth;
                ImgPreview.Height = bi.PixelHeight;
                DrawingCanvas.Width = bi.PixelWidth;
                DrawingCanvas.Height = bi.PixelHeight;
            }
        }

        private void BtnCopyPath_Click(object sender, RoutedEventArgs e)
        {
            Utils.ClipboardHelper.CopyText(_filePath);
            MessageBox.Show("Path copied to clipboard!", "UNICUT", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            Utils.FileHelper.OpenFolderAndSelectFile(_filePath);
        }

        private void DrawingCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (RbDraw.IsChecked == true)
            {
                DrawingCanvas.EditingMode = InkCanvasEditingMode.Ink;
                return;
            }
            
            DrawingCanvas.EditingMode = InkCanvasEditingMode.None;

            if (RbRectangle.IsChecked == true)
            {
                _isDrawingShape = true;
                _startPoint = e.GetPosition(DrawingCanvas);
                _currentRect = new Rectangle
                {
                    Stroke = Brushes.Red,
                    StrokeThickness = 3,
                    Fill = Brushes.Transparent
                };
                InkCanvas.SetLeft(_currentRect, _startPoint.X);
                InkCanvas.SetTop(_currentRect, _startPoint.Y);
                DrawingCanvas.Children.Add(_currentRect);
            }
            else if (RbText.IsChecked == true)
            {
                var pos = e.GetPosition(DrawingCanvas);
                var tb = new TextBox
                {
                    Text = "Text",
                    Foreground = Brushes.Red,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    FontSize = 24,
                    AcceptsReturn = true
                };
                InkCanvas.SetLeft(tb, pos.X);
                InkCanvas.SetTop(tb, pos.Y);
                DrawingCanvas.Children.Add(tb);
                tb.Focus();
                tb.SelectAll();
            }
        }

        private void DrawingCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDrawingShape && RbRectangle.IsChecked == true && _currentRect != null)
            {
                var pos = e.GetPosition(DrawingCanvas);
                var x = Math.Min(pos.X, _startPoint.X);
                var y = Math.Min(pos.Y, _startPoint.Y);
                var w = Math.Max(pos.X, _startPoint.X) - x;
                var h = Math.Max(pos.Y, _startPoint.Y) - y;

                InkCanvas.SetLeft(_currentRect, x);
                InkCanvas.SetTop(_currentRect, y);
                _currentRect.Width = w;
                _currentRect.Height = h;
            }
        }

        private void DrawingCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDrawingShape)
            {
                _isDrawingShape = false;
                _currentRect = null;
            }
        }
    }
}
