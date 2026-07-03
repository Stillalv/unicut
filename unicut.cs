using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Windows.Forms;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Brushes = System.Windows.Media.Brushes;
using Rectangle = System.Windows.Shapes.Rectangle;
using Point = System.Windows.Point;

namespace UNICUT
{
    // ================= ENTRY POINT =================
    public class Program
    {
        [STAThread]
        public static void Main()
        {
            bool createdNew;
            using (var waitHandle = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, "UNICUT_TRIGGER", out createdNew))
            {
                if (!createdNew)
                {
                    waitHandle.Set();
                    return;
                }

                var app = new Application();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var trayManager = new TrayManager();

                System.Threading.ThreadPool.QueueUserWorkItem((state) => {
                    while (true)
                    {
                        waitHandle.WaitOne();
                        Application.Current.Dispatcher.Invoke(new Action(() => {
                            trayManager.ShowWidget();
                        }));
                    }
                });

                app.Startup += (s, e) => {
                    trayManager.Init();
                };
                app.Run();
            }
        }
    }

    // ================= TRAY & HOTKEY =================
    public class TrayManager
    {
        private NotifyIcon _notifyIcon;
        private HotkeyService _hotkeyService;
        private OverlayWindow _overlayWindow;
        private FloatingWidgetWindow _widget;

        public void Init()
        {
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
            _notifyIcon.Visible = true;
            _notifyIcon.Text = "UNICUT (Ctrl+Shift+S)";
            
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("About UNICUT", null, (s, args) => ShowAbout());
            contextMenu.Items.Add("Capture Now (Ctrl+Shift+S)", null, (s, args) => OnHotkeyPressed());
            contextMenu.Items.Add("Show Floating Widget", null, (s, args) => ShowWidget());
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Exit", null, (s, args) => ExitApplication());
            _notifyIcon.ContextMenuStrip = contextMenu;

            _notifyIcon.DoubleClick += (s, args) => ShowAbout();

            _hotkeyService = new HotkeyService();
            _hotkeyService.HotkeyPressed += OnHotkeyPressed;
            _hotkeyService.RegisterHotkey();

            _notifyIcon.ShowBalloonTip(3000, "UNICUT is Running", "Press Ctrl+Shift+S, click the tray icon, or use the floating widget to capture.", ToolTipIcon.Info);

            _widget = new FloatingWidgetWindow(() => OnHotkeyPressed());
            _widget.Show();
        }

        public void ShowWidget()
        {
            if (_widget != null) {
                _widget.Show();
                _widget.Activate();
            }
        }

        private void ShowAbout()
        {
            MessageBox.Show("UNICUT is running in the background.\n\nHotkey: Ctrl + Shift + S\n\nCaptured images are saved to your Pictures/UNICUT folder and copied to your clipboard.", "About UNICUT", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnHotkeyPressed()
        {
            if (_overlayWindow == null || !_overlayWindow.IsLoaded)
            {
                _overlayWindow = new OverlayWindow();
                _overlayWindow.Closed += (s, e) => _overlayWindow = null;
                _overlayWindow.Show();
            }
            else
            {
                _overlayWindow.Activate();
            }
        }

        private void ExitApplication()
        {
            if (_widget != null) { _widget.Close(); _widget = null; }
            if (_hotkeyService != null) _hotkeyService.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            Application.Current.Shutdown();
        }
    }

    public class HotkeyService : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID = 9000;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint VK_S = 0x53; 
        private const int WM_HOTKEY = 0x0312;

        public event Action HotkeyPressed;

        public void RegisterHotkey()
        {
            bool registered = RegisterHotKey(IntPtr.Zero, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_S);
            if (registered)
            {
                ComponentDispatcher.ThreadPreprocessMessage += ComponentDispatcher_ThreadPreprocessMessage;
            }
        }

        private void ComponentDispatcher_ThreadPreprocessMessage(ref MSG msg, ref bool handled)
        {
            if (msg.message == WM_HOTKEY && msg.wParam.ToInt32() == HOTKEY_ID)
            {
                if (HotkeyPressed != null) HotkeyPressed();
                handled = true;
            }
        }

        public void Dispose()
        {
            UnregisterHotKey(IntPtr.Zero, HOTKEY_ID);
            ComponentDispatcher.ThreadPreprocessMessage -= ComponentDispatcher_ThreadPreprocessMessage;
        }
    }

    // ================= CORE SERVICES =================
    public class CaptureService
    {
        public Bitmap CaptureRegion(System.Drawing.Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return null;
            Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            }
            return bitmap;
        }
    }

    public static class GlobalEvents
    {
        public static event Action<string> OnCaptureSaved;
        public static void NotifyCaptureSaved(string filePath)
        {
            if (OnCaptureSaved != null)
                OnCaptureSaved(filePath);
        }
    }

    public class SaveService
    {
        public string SaveBitmap(Bitmap bitmap)
        {
            try
            {
                string picturesFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                string unicutFolder = System.IO.Path.Combine(picturesFolder, "UNICUT");
                if (!Directory.Exists(unicutFolder)) Directory.CreateDirectory(unicutFolder);
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filePath = System.IO.Path.Combine(unicutFolder, "unicut_" + timestamp + ".png");
                bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                return filePath;
            }
            catch { return null; }
        }
    }

    public static class ClipboardHelper
    {
        public static void CopyText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            try { System.Windows.Clipboard.SetText(text); } catch { }
        }

        public static void CopyImage(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            try { 
                var bitmap = new BitmapImage(new Uri(filePath, UriKind.Absolute));
                System.Windows.Clipboard.SetImage(bitmap); 
            } catch { }
        }
    }

    public static class FileHelper
    {
        public static void OpenFolderAndSelectFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = "/select,\"" + filePath + "\"", UseShellExecute = true });
        }
    }

    public class FloatingWidgetWindow : Window
    {
        private StackPanel _historyStack;

        public FloatingWidgetWindow(Action onCapture)
        {
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.Topmost = true;
            this.ShowInTaskbar = false;
            this.Width = 88;
            this.SizeToContent = SizeToContent.Height;

            this.Left = SystemParameters.WorkArea.Left + 20;
            
            this.SizeChanged += (s, e) => {
                if (e.HeightChanged) {
                    this.Top = SystemParameters.WorkArea.Bottom - this.ActualHeight - 20;
                }
            };

            var mainStack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Vertical };

            var historyBorder = new Border
            {
                Margin = new Thickness(0, 0, 0, 10),
                Visibility = Visibility.Collapsed
            };

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, MaxHeight = 300, Margin = new Thickness(5) };
            _historyStack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Vertical };
            scroll.Content = _historyStack;
            historyBorder.Child = scroll;

            var buttonBorder = new Border
            {
                Height = 88,
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 32, 32, 32)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(150, 65, 65, 65)),
                BorderThickness = new Thickness(1),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 10, Opacity = 0.5, ShadowDepth = 2 }
            };

            var btnGrid = new Grid { HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = System.Windows.VerticalAlignment.Center };
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            btnGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            btnGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            var btnCapture = CreateWidgetSvgButton("M14.5 4h-5L7 7H4a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2h-3l-2.5-3z M12 16a4 4 0 1 0 0-8 4 4 0 0 0 0 8z", "Capture", 32, 32);
            btnCapture.Click += (s, e) => onCapture();

            var btnToggleHistory = CreateWidgetSvgButton("M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z M12 9a3 3 0 1 0 0 6 3 3 0 1 0 0-6z", "Toggle History", 32, 32);
            var btnOpen = CreateWidgetSvgButton("M4 20h16a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.93a2 2 0 0 1-1.66-.9l-.82-1.2A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13c0 1.1.9 2 2 2Z M2 10h20", "Open Existing Image", 32, 32);
            btnOpen.Click += (s, e) => {
                string folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "UNICUT");
                if (System.IO.Directory.Exists(folder)) {
                    var lastFile = new System.IO.DirectoryInfo(folder).GetFiles("*.png").OrderByDescending(f => f.CreationTime).FirstOrDefault();
                    if (lastFile != null) {
                        var editor = new EditorPopup(lastFile.FullName);
                        editor.ShowDialog();
                        return;
                    }
                }
                System.Windows.MessageBox.Show("No captures found.", "UNICUT", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            };
            var btnClose = CreateWidgetSvgButton("M18 6 6 18 M6 6l12 12", "Hide Widget", 32, 32, 11);
            
            btnCapture.Margin = new Thickness(2);
            btnOpen.Margin = new Thickness(2);
            btnToggleHistory.Margin = new Thickness(2);
            btnClose.Margin = new Thickness(2);

            Grid.SetRow(btnCapture, 0); Grid.SetColumn(btnCapture, 0);
            Grid.SetRow(btnOpen, 0); Grid.SetColumn(btnOpen, 1);
            Grid.SetRow(btnToggleHistory, 1); Grid.SetColumn(btnToggleHistory, 0);
            Grid.SetRow(btnClose, 1); Grid.SetColumn(btnClose, 1);

            string pathEye = "M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z M12 9a3 3 0 1 0 0 6 3 3 0 1 0 0-6z";
            string pathEyeOff = "M9.88 9.88a3 3 0 1 0 4.24 4.24 M10.73 5.08A10.43 10.43 0 0 1 12 5c7 0 10 7 10 7a13.16 13.16 0 0 1-1.67 2.68 M6.61 6.61A13.526 13.526 0 0 0 2 12s3 7 10 7a9.74 9.74 0 0 0 5.39-1.61 M2 2l20 20";

            btnToggleHistory.Click += (s, e) => {
                if (btnCapture.Visibility == Visibility.Visible) {
                    btnCapture.Visibility = Visibility.Collapsed;
                    btnOpen.Visibility = Visibility.Collapsed;
                    btnClose.Visibility = Visibility.Collapsed;
                    historyBorder.Visibility = Visibility.Collapsed;
                    this.Width = 44;
                    buttonBorder.Height = 44;
                    buttonBorder.CornerRadius = new CornerRadius(22);
                    ((System.Windows.Shapes.Path)btnToggleHistory.Content).Data = System.Windows.Media.Geometry.Parse(pathEyeOff);
                } else {
                    btnCapture.Visibility = Visibility.Visible;
                    btnOpen.Visibility = Visibility.Visible;
                    btnClose.Visibility = Visibility.Visible;
                    this.Width = 88;
                    buttonBorder.Height = 88;
                    buttonBorder.CornerRadius = new CornerRadius(16);
                    ((System.Windows.Shapes.Path)btnToggleHistory.Content).Data = System.Windows.Media.Geometry.Parse(pathEye);
                    if (_historyStack.Children.Count > 0) {
                        historyBorder.Visibility = Visibility.Visible;
                        scroll.ScrollToBottom();
                    }
                }
            };
            btnClose.Click += (s, e) => {
                _historyStack.Children.Clear();
                historyBorder.Visibility = Visibility.Collapsed;
                ((System.Windows.Shapes.Path)btnToggleHistory.Content).Data = System.Windows.Media.Geometry.Parse(pathEye);
                this.Hide();
            };

            btnCapture.MouseEnter += (s, e) => btnCapture.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 0, 120, 215));
            btnCapture.MouseLeave += (s, e) => btnCapture.Background = Brushes.Transparent;
            btnOpen.MouseEnter += (s, e) => btnOpen.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 0, 120, 215));
            btnOpen.MouseLeave += (s, e) => btnOpen.Background = Brushes.Transparent;
            btnToggleHistory.MouseEnter += (s, e) => btnToggleHistory.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 128, 128, 128));
            btnToggleHistory.MouseLeave += (s, e) => btnToggleHistory.Background = Brushes.Transparent;
            btnClose.MouseEnter += (s, e) => { btnClose.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 53, 69)); ((System.Windows.Shapes.Path)btnClose.Content).Stroke = Brushes.White; };
            btnClose.MouseLeave += (s, e) => { btnClose.Background = Brushes.Transparent; };

            btnGrid.Children.Add(btnCapture);
            btnGrid.Children.Add(btnOpen);
            btnGrid.Children.Add(btnToggleHistory);
            btnGrid.Children.Add(btnClose);
            buttonBorder.Child = btnGrid;

            buttonBorder.MouseLeftButtonDown += (s, e) => this.DragMove();

            mainStack.Children.Add(historyBorder);
            mainStack.Children.Add(buttonBorder);

            this.Content = mainStack;

            GlobalEvents.OnCaptureSaved += (filePath) => {
                Application.Current.Dispatcher.Invoke(new Action(() => {
                    historyBorder.Visibility = Visibility.Visible;
                    var thumbBorder = new Border {
                        Margin = new Thickness(0, 0, 0, 10),
                        CornerRadius = new CornerRadius(8),
                        Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 32, 32, 32)),
                        BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(150, 65, 65, 65)),
                        BorderThickness = new Thickness(1),
                        ClipToBounds = true,
                        Cursor = System.Windows.Input.Cursors.Hand,
                        ToolTip = "Click to Copy / Drag to Drop",
                        Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 10, Opacity = 0.5, ShadowDepth = 2 },
                        Padding = new Thickness(4)
                    };
                    
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.DecodePixelWidth = 80;
                    bi.UriSource = new Uri(filePath);
                    bi.EndInit();

                    var thumbGrid = new Grid();
                    var img = new System.Windows.Controls.Image { Source = bi, Width = 70, Height = 50, Stretch = Stretch.UniformToFill };
                    thumbGrid.Children.Add(img);

                    var closeBtn = new System.Windows.Controls.Button {
                        Width = 16, Height = 16,
                        Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 220, 53, 69)),
                        BorderThickness = new Thickness(0),
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                        VerticalAlignment = System.Windows.VerticalAlignment.Top,
                        Margin = new Thickness(0,-2,-2,0),
                        Cursor = System.Windows.Input.Cursors.Hand, ToolTip = "Remove from history"
                    };
                    var path = new System.Windows.Shapes.Path {
                        Data = System.Windows.Media.Geometry.Parse("M18 6 6 18 M6 6l12 12"),
                        Stroke = Brushes.White, StrokeThickness = 1.5,
                        StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = System.Windows.VerticalAlignment.Center,
                        Stretch = Stretch.Uniform, Width = 8, Height = 8
                    };
                    closeBtn.Content = path;
                    ApplyIconTemplate(closeBtn);
                    
                    closeBtn.MouseEnter += (senderBtn, args) => closeBtn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 50, 50));
                    closeBtn.MouseLeave += (senderBtn, args) => closeBtn.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 220, 53, 69));

                    closeBtn.Click += (senderBtn, args) => {
                        _historyStack.Children.Remove(thumbBorder);
                    };

                    thumbGrid.Children.Add(closeBtn);
                    thumbBorder.Child = thumbGrid;

                    Point? dragStartPoint = null;

                    thumbBorder.PreviewMouseLeftButtonDown += (s, e) => {
                        dragStartPoint = e.GetPosition(null);
                    };

                    thumbBorder.PreviewMouseMove += (s, e) => {
                        if (e.LeftButton == MouseButtonState.Pressed && dragStartPoint.HasValue) {
                            var pos = e.GetPosition(null);
                            if (Math.Abs(pos.X - dragStartPoint.Value.X) > SystemParameters.MinimumHorizontalDragDistance ||
                                Math.Abs(pos.Y - dragStartPoint.Value.Y) > SystemParameters.MinimumVerticalDragDistance) {
                                
                                var data = new System.Windows.DataObject();
                                data.SetData(System.Windows.DataFormats.FileDrop, new string[] { filePath });
                                data.SetData(System.Windows.DataFormats.Text, filePath);
                                System.Windows.DragDrop.DoDragDrop(thumbBorder, data, System.Windows.DragDropEffects.Copy);
                                dragStartPoint = null;
                            }
                        }
                    };

                    thumbBorder.PreviewMouseLeftButtonUp += (s, e) => {
                        dragStartPoint = null;
                        ClipboardHelper.CopyImage(filePath);
                    };

                    _historyStack.Children.Add(thumbBorder);
                    scroll.ScrollToBottom();
                    
                    btnCapture.Visibility = Visibility.Visible;
                    btnOpen.Visibility = Visibility.Visible;
                    btnClose.Visibility = Visibility.Visible;
                    this.Width = 88;
                    buttonBorder.Height = 88;
                    buttonBorder.CornerRadius = new CornerRadius(16);
                    ((System.Windows.Shapes.Path)btnToggleHistory.Content).Data = System.Windows.Media.Geometry.Parse("M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z M12 9a3 3 0 1 0 0 6 3 3 0 1 0 0-6z");
                }));
            };
        }

        private System.Windows.Controls.Button CreateWidgetSvgButton(string pathData, string tooltip, double width, double height, double iconSize = 16)
        {
            var btn = new System.Windows.Controls.Button
            {
                Width = width, Height = height, Margin = new Thickness(0,0,5,0),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand, ToolTip = tooltip
            };
            
            var path = new System.Windows.Shapes.Path {
                Data = System.Windows.Media.Geometry.Parse(pathData),
                Stroke = Brushes.White, StrokeThickness = 1.8,
                StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Stretch = Stretch.Uniform, Width = iconSize, Height = iconSize
            };
            btn.Content = path;

            ApplyIconTemplate(btn);
            return btn;
        }

        private void ApplyIconTemplate(System.Windows.Controls.Button btn)
        {
            var template = new ControlTemplate(typeof(System.Windows.Controls.Button));
            var b = new FrameworkElementFactory(typeof(Border));
            b.SetValue(Border.CornerRadiusProperty, new CornerRadius(16));
            b.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(System.Windows.Controls.Button.BackgroundProperty));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
            b.AppendChild(cp);
            template.VisualTree = b;
            btn.Template = template;
        }
    }

    public class OverlayWindow : Window
    {
        private Canvas OverlayCanvas;
        private Rectangle SelectionRectangle;
        private Point _startPoint;
        private bool _isSelecting;
        private Border _topPanel;

        public OverlayWindow()
        {
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x60, 0x00, 0x00, 0x00));
            this.Topmost = true;
            this.ShowInTaskbar = false;
            this.Cursor = System.Windows.Input.Cursors.Arrow;

            OverlayCanvas = new Canvas();
            SelectionRectangle = new Rectangle {
                Stroke = Brushes.White,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 4 },
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)),
                Visibility = Visibility.Collapsed
            };
            OverlayCanvas.Children.Add(SelectionRectangle);

            _topPanel = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(70, 70, 70)),
                BorderThickness = new Thickness(1),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 15, Opacity = 0.5, ShadowDepth = 2 },
                Padding = new Thickness(5),
                Cursor = System.Windows.Input.Cursors.Arrow
            };

            var stack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            
            var fontFam = new System.Windows.Media.FontFamily("Segoe UI Variable Text, Segoe UI, sans-serif");
            
            var lblRegion = new System.Windows.Controls.TextBlock { 
                Text = "Drag to cut region or:", 
                Foreground = Brushes.LightGray, 
                FontFamily = fontFam,
                FontSize = 13,
                VerticalAlignment = System.Windows.VerticalAlignment.Center, 
                Margin = new Thickness(10,0,15,0) 
            };

            var btnRegion = CreateStyledButton("Region", 85, System.Windows.Media.Color.FromRgb(0, 120, 215), System.Windows.Media.Color.FromRgb(30, 150, 255), fontFam);
            btnRegion.Margin = new Thickness(0,0,8,0);
            
            var btnFull = CreateStyledButton("Fullscreen", 85, System.Windows.Media.Color.FromRgb(55, 55, 55), System.Windows.Media.Color.FromRgb(75, 75, 75), fontFam);
            btnFull.Margin = new Thickness(0,0,8,0);

            var btnCancel = CreateStyledButton("Cancel", 70, System.Windows.Media.Color.FromRgb(55, 55, 55), System.Windows.Media.Color.FromRgb(220, 53, 69), fontFam);

            btnFull.Click += async (s,e) => {
                this.Hide();
                await System.Threading.Tasks.Task.Delay(150);
                
                var bounds = new System.Drawing.Rectangle(
                    System.Windows.Forms.SystemInformation.VirtualScreen.Left,
                    System.Windows.Forms.SystemInformation.VirtualScreen.Top,
                    System.Windows.Forms.SystemInformation.VirtualScreen.Width,
                    System.Windows.Forms.SystemInformation.VirtualScreen.Height);
                    
                if (bounds.Width <= 0) bounds.Width = 1920;
                if (bounds.Height <= 0) bounds.Height = 1080;

                PerformCapture(bounds);
            };

            btnRegion.Click += (s,e) => {
                _topPanel.Visibility = Visibility.Collapsed;
                this.Cursor = System.Windows.Input.Cursors.Cross;
            };

            btnCancel.Click += (s,e) => this.Close();

            stack.Children.Add(lblRegion);
            stack.Children.Add(btnRegion);
            stack.Children.Add(btnFull);
            stack.Children.Add(btnCancel);
            _topPanel.Child = stack;

            OverlayCanvas.Children.Add(_topPanel);
            this.Content = OverlayCanvas;

            this.Loaded += Window_Loaded;
            this.MouseDown += Window_MouseDown;
            this.MouseMove += Window_MouseMove;
            this.MouseUp += Window_MouseUp;
            this.KeyDown += Window_KeyDown;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Left = SystemParameters.VirtualScreenLeft;
            this.Top = SystemParameters.VirtualScreenTop;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;

            _topPanel.UpdateLayout();
            Canvas.SetLeft(_topPanel, (this.Width - _topPanel.ActualWidth) / 2);
            Canvas.SetTop(_topPanel, 20);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_topPanel.IsMouseOver && _topPanel.Visibility == Visibility.Visible)
                return;

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _topPanel.Visibility = Visibility.Collapsed;
                _startPoint = e.GetPosition(OverlayCanvas);
                _isSelecting = true;
                SelectionRectangle.Visibility = Visibility.Visible;
                Canvas.SetLeft(SelectionRectangle, _startPoint.X);
                Canvas.SetTop(SelectionRectangle, _startPoint.Y);
                SelectionRectangle.Width = 0;
                SelectionRectangle.Height = 0;
                OverlayCanvas.CaptureMouse();
            }
        }

        private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isSelecting)
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
            if (_isSelecting)
            {
                _isSelecting = false;
                OverlayCanvas.ReleaseMouseCapture();

                int w = (int)SelectionRectangle.Width;
                int h = (int)SelectionRectangle.Height;

                if (w > 10 && h > 10)
                {
                    int x = (int)Canvas.GetLeft(SelectionRectangle) + (int)this.Left;
                    int y = (int)Canvas.GetTop(SelectionRectangle) + (int)this.Top;
                    PerformCapture(new System.Drawing.Rectangle(x, y, w, h));
                }
                else
                {
                    this.Close();
                }
            }
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape) this.Close();
        }

        private void PerformCapture(System.Drawing.Rectangle bounds)
        {
            try
            {
                this.Hide();

                var captureService = new CaptureService();
                var bitmap = captureService.CaptureRegion(bounds);

                if (bitmap != null)
                {
                    var editorPopup = new EditorPopup(bitmap);
                    editorPopup.Show();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Capture failed: " + ex.Message, "UNICUT Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.Close();
            }
        }

        private System.Windows.Controls.Button CreateStyledButton(string text, double width, System.Windows.Media.Color bgColor, System.Windows.Media.Color hoverColor, System.Windows.Media.FontFamily font)
        {
            var btn = new System.Windows.Controls.Button
            {
                Content = text, Width = width, Height = 30, Foreground = Brushes.White, Background = new SolidColorBrush(bgColor),
                BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand, FontFamily = font, FontSize = 13, FontWeight = FontWeights.SemiBold
            };
            var template = new ControlTemplate(typeof(System.Windows.Controls.Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(System.Windows.Controls.Button.BackgroundProperty));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
            border.AppendChild(cp);
            template.VisualTree = border;
            btn.Template = template;
            btn.MouseEnter += (s, e) => btn.Background = new SolidColorBrush(hoverColor);
            btn.MouseLeave += (s, e) => btn.Background = new SolidColorBrush(bgColor);
            return btn;
        }
    }

    public enum EditorTool { Draw, Rectangle, Circle, Arrow, Text }

    public class EditorPopup : Window
    {
        private string _filePath;
        
        private Point _startPoint;
        private Rectangle _currentRect;
        private System.Windows.Shapes.Ellipse _currentEllipse;
        private Line _currentLine;
        private Polygon _currentArrowHead;
        private bool _isDrawingShape;

        private InkCanvas DrawingCanvas;
        private System.Windows.Controls.Image ImgPreview;
        
        private EditorTool _currentTool = EditorTool.Draw;
        private System.Windows.Controls.Button BtnDraw;
        private System.Windows.Controls.Button BtnRectangle;
        private System.Windows.Controls.Button BtnCircle;
        private System.Windows.Controls.Button BtnArrow;
        private System.Windows.Controls.Button BtnText;
        private System.Windows.Controls.Button BtnSaveCopy;
        private System.Windows.Controls.Button BtnUndo;
        private System.Windows.Controls.Button BtnRedo;
        private StackPanel _historyStack;
        private System.Drawing.Bitmap _initialBitmap;
        private UndoRedoManager _undoRedoManager;

        public EditorPopup(string filePath) : this((System.Drawing.Bitmap)null)
        {
            _filePath = filePath;
        }

        public EditorPopup(System.Drawing.Bitmap bitmap)
        {
            _undoRedoManager = new UndoRedoManager();
            _undoRedoManager.StateChanged += UpdateUndoRedoButtons;
            _filePath = null;
            _initialBitmap = bitmap;

            this.Title = "UNICUT Editor";
            this.Width = 1000;
            this.Height = 700;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.Topmost = true;
            try { 
                string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.ico");
                if (System.IO.File.Exists(iconPath)) {
                    this.Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath));
                }
            } catch { }

            var mainBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 32, 32)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(65, 65, 65)),
                BorderThickness = new Thickness(1),
                ClipToBounds = true,
                Margin = new Thickness(15),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 20, Opacity = 0.6, ShadowDepth = 5 }
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) }); // Header
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content Area

            // 1. Integrated Header (Fluent Icon-Only)
            var headerBar = new Grid { Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 32, 32)) };
            headerBar.MouseLeftButtonDown += (s, e) => this.DragMove();

            var leftPanelHeader = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Left, VerticalAlignment = System.Windows.VerticalAlignment.Center, Margin = new Thickness(10,0,0,0) };
            var btnToggleSidebar = CreateSvgButton("M3 12h18 M3 6h18 M3 18h18", "Toggle History Sidebar", false);
            leftPanelHeader.Children.Add(btnToggleSidebar);

            var toolbarPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = System.Windows.VerticalAlignment.Center };
            
            BtnDraw = CreateSvgButton("M17 3a2.85 2.83 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5Z", "Draw (Pen)", false);
            BtnRectangle = CreateSvgButton("M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z", "Rectangle", false);
            BtnCircle = CreateSvgButton("M12 22c5.523 0 10-4.477 10-10S17.523 2 12 2 2 6.477 2 12s4.477 10 10 10z", "Circle", false);
            BtnArrow = CreateSvgButton("M7 17L17 7 M7 7h10v10", "Arrow", false);
            BtnText = CreateSvgButton("M4 7V4h16v3 M9 20h6 M12 4v16", "Text", false);

            BtnDraw.Click += (s, e) => SetTool(EditorTool.Draw);
            BtnRectangle.Click += (s, e) => SetTool(EditorTool.Rectangle);
            BtnCircle.Click += (s, e) => SetTool(EditorTool.Circle);
            BtnArrow.Click += (s, e) => SetTool(EditorTool.Arrow);
            BtnText.Click += (s, e) => SetTool(EditorTool.Text);

            BtnText.Click += (s, e) => SetTool(EditorTool.Text);

            BtnUndo = CreateSvgButton("M3 7v6h6 M21 17a9 9 0 0 0-9-9 9 9 0 0 0-6 2.3L3 13", "Undo (Ctrl+Z)", false);
            BtnRedo = CreateSvgButton("M21 7v6h-6 M3 17a9 9 0 0 1 9-9 9 9 0 0 1 6 2.3l3 2.7", "Redo (Ctrl+Y)", false);
            BtnUndo.Click += (s, e) => _undoRedoManager.Undo();
            BtnRedo.Click += (s, e) => _undoRedoManager.Redo();

            var separator = new Border { Width = 1, Height = 20, Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(65, 65, 65)), Margin = new Thickness(10, 0, 10, 0) };

            toolbarPanel.Children.Add(BtnDraw);
            toolbarPanel.Children.Add(BtnRectangle);
            toolbarPanel.Children.Add(BtnCircle);
            toolbarPanel.Children.Add(BtnArrow);
            toolbarPanel.Children.Add(BtnText);
            toolbarPanel.Children.Add(separator);
            toolbarPanel.Children.Add(BtnUndo);
            toolbarPanel.Children.Add(BtnRedo);
            
            UpdateUndoRedoButtons();
            
            var rightPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, VerticalAlignment = System.Windows.VerticalAlignment.Center, Margin = new Thickness(0,0,10,0) };
            
            var btnFolder = CreateSvgButton("M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z", "Open Folder", false);
            btnFolder.Click += (s, e) => FileHelper.OpenFolderAndSelectFile(_filePath);
            
            BtnSaveCopy = CreateSvgButton("M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z M17 21V13H7v8 M7 3v5h8", "Save & Copy to Clipboard", true);
            BtnSaveCopy.Click += (s, e) => SaveEditedImage();

            var btnClose = CreateSvgButton("M18 6L6 18 M6 6l12 12", "Close", false);
            btnClose.MouseEnter += (s, e) => { btnClose.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 53, 69)); ((System.Windows.Shapes.Path)btnClose.Content).Stroke = Brushes.White; };
            btnClose.MouseLeave += (s, e) => { btnClose.Background = Brushes.Transparent; };
            btnClose.Click += (s, e) => this.Close();

            rightPanel.Children.Add(btnFolder);
            rightPanel.Children.Add(BtnSaveCopy);
            rightPanel.Children.Add(btnClose);

            headerBar.Children.Add(leftPanelHeader);
            headerBar.Children.Add(toolbarPanel);
            headerBar.Children.Add(rightPanel);

            var mainLayoutGrid = new Grid();
            mainLayoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Sidebar
            mainLayoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Editor

            // 2. Editor Area
            var editorContainer = new Border { Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(20, 20, 20)), BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)), BorderThickness = new Thickness(0, 1, 0, 1) };
            var viewbox = new Viewbox { Stretch = Stretch.Uniform, Margin = new Thickness(20) };
            var contentGrid = new Grid { HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = System.Windows.VerticalAlignment.Center };
            
            var imageShadow = new Border { Background = Brushes.White, Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 25, Opacity = 0.2, ShadowDepth = 10 } };

            var renderGrid = new Grid { HorizontalAlignment = System.Windows.HorizontalAlignment.Left, VerticalAlignment = System.Windows.VerticalAlignment.Top };
            ImgPreview = new System.Windows.Controls.Image { Stretch = Stretch.None, HorizontalAlignment = System.Windows.HorizontalAlignment.Left, VerticalAlignment = System.Windows.VerticalAlignment.Top };
            DrawingCanvas = new InkCanvas { Background = Brushes.Transparent, HorizontalAlignment = System.Windows.HorizontalAlignment.Left, VerticalAlignment = System.Windows.VerticalAlignment.Top };
            DrawingCanvas.DefaultDrawingAttributes.Color = System.Windows.Media.Colors.Red;
            DrawingCanvas.DefaultDrawingAttributes.Width = 4;
            DrawingCanvas.DefaultDrawingAttributes.Height = 4;

            DrawingCanvas.PreviewMouseLeftButtonDown += DrawingCanvas_MouseDown;
            DrawingCanvas.PreviewMouseMove += DrawingCanvas_MouseMove;
            DrawingCanvas.PreviewMouseLeftButtonUp += DrawingCanvas_MouseUp;
            DrawingCanvas.StrokeCollected += (s, e) => _undoRedoManager.AddAction(new InkStrokeAction(DrawingCanvas, e.Stroke));

            renderGrid.Children.Add(ImgPreview);
            renderGrid.Children.Add(DrawingCanvas);

            contentGrid.Children.Add(imageShadow);
            contentGrid.Children.Add(renderGrid);
            viewbox.Child = contentGrid;
            editorContainer.Child = viewbox;

            // 3. History Sidebar
            var historyBorder = new Border { Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(28, 28, 28)), BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)), BorderThickness = new Thickness(0, 1, 1, 1), Width = 170 };
            var historyPanel = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(10, 10, 5, 10) };
            
            string scrollStyleXaml = @"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
       xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
       TargetType='ScrollBar'>
    <Setter Property='Background' Value='Transparent' />
    <Setter Property='Width' Value='6' />
    <Setter Property='Template'>
        <Setter.Value>
            <ControlTemplate TargetType='ScrollBar'>
                <Grid Background='Transparent'>
                    <Track Name='PART_Track' IsDirectionReversed='true'>
                        <Track.Thumb>
                            <Thumb>
                                <Thumb.Template>
                                    <ControlTemplate TargetType='Thumb'>
                                        <Border Background='#666666' CornerRadius='3' />
                                    </ControlTemplate>
                                </Thumb.Template>
                            </Thumb>
                        </Track.Thumb>
                    </Track>
                </Grid>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>";
            var scrollStyle = (Style)System.Windows.Markup.XamlReader.Parse(scrollStyleXaml);
            historyPanel.Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar), scrollStyle);

            historyPanel.PreviewMouseWheel += (s, e) => {
                historyPanel.ScrollToVerticalOffset(historyPanel.VerticalOffset - e.Delta);
                e.Handled = true;
            };
            
            _historyStack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Vertical };
            historyPanel.Content = _historyStack;
            historyBorder.Child = historyPanel;

            btnToggleSidebar.Click += (s, e) => {
                historyBorder.Visibility = historyBorder.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            };

            Grid.SetColumn(historyBorder, 0);
            Grid.SetColumn(editorContainer, 1);
            mainLayoutGrid.Children.Add(historyBorder);
            mainLayoutGrid.Children.Add(editorContainer);

            Grid.SetRow(headerBar, 0);
            Grid.SetRow(mainLayoutGrid, 1);
            grid.Children.Add(headerBar);
            grid.Children.Add(mainLayoutGrid);

            mainBorder.Child = grid;
            this.Content = mainBorder;
            this.Loaded += Window_Loaded;
            this.KeyDown += Window_KeyDown;

            SetTool(EditorTool.Draw);
        }

        private void UpdateUndoRedoButtons()
        {
            if (BtnUndo != null) BtnUndo.Opacity = _undoRedoManager.CanUndo ? 1.0 : 0.4;
            if (BtnRedo != null) BtnRedo.Opacity = _undoRedoManager.CanRedo ? 1.0 : 0.4;
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                    _undoRedoManager.Redo();
                else
                    _undoRedoManager.Undo();
            }
            else if (e.Key == Key.Y && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                _undoRedoManager.Redo();
            }
            else if (e.Key == Key.Escape)
            {
                this.Close();
            }
        }

        private System.Windows.Controls.Button CreateSvgButton(string pathData, string tooltip, bool isAction)
        {
            var btn = new System.Windows.Controls.Button
            {
                Width = 36, Height = 36, Margin = new Thickness(2, 0, 2, 0),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand, ToolTip = tooltip
            };
            
            var path = new System.Windows.Shapes.Path {
                Data = System.Windows.Media.Geometry.Parse(pathData),
                Stroke = Brushes.White, StrokeThickness = 1.8,
                StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Stretch = Stretch.Uniform, Width = 16, Height = 16
            };
            btn.Content = path;
            
            var template = new ControlTemplate(typeof(System.Windows.Controls.Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(System.Windows.Controls.Button.BackgroundProperty));
            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
            border.AppendChild(contentPresenter);
            template.VisualTree = border;
            btn.Template = template;

            if (isAction) {
                btn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215));
                btn.MouseEnter += (s, e) => btn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 150, 255));
                btn.MouseLeave += (s, e) => btn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215));
            } else {
                btn.MouseEnter += (s, e) => { if (btn.Background == Brushes.Transparent) btn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 50, 50)); };
                btn.MouseLeave += (s, e) => { if (((SolidColorBrush)btn.Background).Color == System.Windows.Media.Color.FromRgb(50, 50, 50)) btn.Background = Brushes.Transparent; };
            }
            return btn;
        }

        private void SetTool(EditorTool tool)
        {
            _currentTool = tool;
            var normalBg = Brushes.Transparent;
            var activeBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(70, 70, 70)); // Light highlight

            BtnDraw.Background = tool == EditorTool.Draw ? activeBg : normalBg;
            BtnRectangle.Background = tool == EditorTool.Rectangle ? activeBg : normalBg;
            BtnArrow.Background = tool == EditorTool.Arrow ? activeBg : normalBg;
            BtnText.Background = tool == EditorTool.Text ? activeBg : normalBg;

            if (DrawingCanvas != null)
                DrawingCanvas.EditingMode = tool == EditorTool.Draw ? InkCanvasEditingMode.Ink : InkCanvasEditingMode.None;
        }

        private void SaveEditedImage()
        {
            try
            {
                SetTool(EditorTool.Draw);
                
                var parentGrid = (Grid)ImgPreview.Parent;
                parentGrid.UpdateLayout();

                int width = (int)ImgPreview.ActualWidth;
                int height = (int)ImgPreview.ActualHeight;

                if (width <= 0 || height <= 0) return;

                RenderTargetBitmap rtb = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Pbgra32);
                rtb.Render(parentGrid);

                System.Drawing.Bitmap bmp;
                using (MemoryStream ms = new MemoryStream())
                {
                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(rtb));
                    encoder.Save(ms);
                    bmp = new System.Drawing.Bitmap(ms);
                }

                var saveService = new SaveService();
                string newFilePath = saveService.SaveBitmap(bmp);
                
                if (!string.IsNullOrEmpty(newFilePath))
                {
                    _filePath = newFilePath;
                    ClipboardHelper.CopyImage(newFilePath);
                    if (BtnSaveCopy != null) 
                    {
                        var path = BtnSaveCopy.Content as System.Windows.Shapes.Path;
                        if (path != null) path.Data = System.Windows.Media.Geometry.Parse("M20 6 9 17l-5-5");
                    }
                    GlobalEvents.NotifyCaptureSaved(newFilePath);
                    LoadHistory();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to save: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialBitmap != null)
            {
                LoadBitmapToEditor(_initialBitmap);
            }
            else if (!string.IsNullOrEmpty(_filePath))
            {
                LoadImageToEditor(_filePath);
            }
            LoadHistory();
        }

        private void LoadBitmapToEditor(System.Drawing.Bitmap bmp)
        {
            try {
                using (MemoryStream ms = new MemoryStream())
                {
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Position = 0;
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.StreamSource = ms;
                    bi.EndInit();

                    ImgPreview.Source = bi;
                    ImgPreview.Width = bi.PixelWidth;
                    ImgPreview.Height = bi.PixelHeight;
                    DrawingCanvas.Width = bi.PixelWidth;
                    DrawingCanvas.Height = bi.PixelHeight;
                    DrawingCanvas.Children.Clear();
                    DrawingCanvas.Strokes.Clear();
                }
            } catch { }
        }

        private void LoadImageToEditor(string path)
        {
            _filePath = path;
            try {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = new Uri(path);
                bi.EndInit();

                ImgPreview.Source = bi;
                ImgPreview.Width = bi.PixelWidth;
                ImgPreview.Height = bi.PixelHeight;
                DrawingCanvas.Width = bi.PixelWidth;
                DrawingCanvas.Height = bi.PixelHeight;
                DrawingCanvas.Children.Clear();
                DrawingCanvas.Strokes.Clear();
            } catch { }
        }

        private void LoadHistory()
        {
            _historyStack.Children.Clear();
            string folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "UNICUT");
            if (!System.IO.Directory.Exists(folder)) return;

            var files = new System.IO.DirectoryInfo(folder).GetFiles("*.png").OrderByDescending(f => f.CreationTime).Take(25);
            foreach (var file in files)
            {
                var border = new Border {
                    Width = 130, Height = 90, Margin = new Thickness(0, 5, 5, 5),
                    CornerRadius = new CornerRadius(6),
                    BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(70, 70, 70)),
                    BorderThickness = new Thickness(1),
                    ClipToBounds = true,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(20, 20, 20))
                };
                border.MouseEnter += (s, e) => border.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215));
                border.MouseLeave += (s, e) => border.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(70, 70, 70));
                
                try {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.UriSource = new Uri(file.FullName);
                    bi.DecodePixelWidth = 120; // memory optimization
                    bi.EndInit();
                    
                    var img = new System.Windows.Controls.Image { Source = bi, Stretch = Stretch.UniformToFill };
                    border.Child = img;

                    string currentFilePath = file.FullName;
                    border.MouseLeftButtonDown += (s, e) => LoadImageToEditor(currentFilePath);

                    _historyStack.Children.Add(border);
                } catch { }
            }
        }

        private void DrawingCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_currentTool == EditorTool.Draw) return;

            if (_currentTool == EditorTool.Rectangle)
            {
                _isDrawingShape = true;
                _startPoint = e.GetPosition(DrawingCanvas);
                _currentRect = new Rectangle
                {
                    Stroke = Brushes.Red,
                    StrokeThickness = 4,
                    Fill = Brushes.Transparent
                };
                InkCanvas.SetLeft(_currentRect, _startPoint.X);
                InkCanvas.SetTop(_currentRect, _startPoint.Y);
                DrawingCanvas.Children.Add(_currentRect);
                DrawingCanvas.CaptureMouse();
            }
            else if (_currentTool == EditorTool.Circle)
            {
                _isDrawingShape = true;
                _startPoint = e.GetPosition(DrawingCanvas);
                _currentEllipse = new System.Windows.Shapes.Ellipse
                {
                    Stroke = Brushes.Red,
                    StrokeThickness = 4,
                    Fill = Brushes.Transparent
                };
                InkCanvas.SetLeft(_currentEllipse, _startPoint.X);
                InkCanvas.SetTop(_currentEllipse, _startPoint.Y);
                DrawingCanvas.Children.Add(_currentEllipse);
                DrawingCanvas.CaptureMouse();
            }
            else if (_currentTool == EditorTool.Arrow)
            {
                _isDrawingShape = true;
                _startPoint = e.GetPosition(DrawingCanvas);
                
                _currentLine = new Line
                {
                    Stroke = Brushes.Red,
                    StrokeThickness = 4,
                    X1 = _startPoint.X,
                    Y1 = _startPoint.Y,
                    X2 = _startPoint.X,
                    Y2 = _startPoint.Y
                };
                
                _currentArrowHead = new Polygon
                {
                    Fill = Brushes.Red,
                    Points = new PointCollection { new Point(0,0), new Point(0,0), new Point(0,0) }
                };

                DrawingCanvas.Children.Add(_currentLine);
                DrawingCanvas.Children.Add(_currentArrowHead);
                DrawingCanvas.CaptureMouse();
            }
            else if (_currentTool == EditorTool.Text)
            {
                var pos = e.GetPosition(DrawingCanvas);
                var tb = new System.Windows.Controls.TextBox
                {
                    Text = "Text",
                    Foreground = Brushes.Red,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    FontSize = 28,
                    FontWeight = FontWeights.Bold,
                    AcceptsReturn = true
                };
                InkCanvas.SetLeft(tb, pos.X);
                InkCanvas.SetTop(tb, pos.Y);
                DrawingCanvas.Children.Add(tb);
                _undoRedoManager.AddAction(new ShapeAction(DrawingCanvas, tb));
                tb.Focus();
                tb.SelectAll();
            }
        }

        private void DrawingCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isDrawingShape && _currentTool == EditorTool.Rectangle && _currentRect != null)
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
            else if (_isDrawingShape && _currentTool == EditorTool.Circle && _currentEllipse != null)
            {
                var pos = e.GetPosition(DrawingCanvas);
                var x = Math.Min(pos.X, _startPoint.X);
                var y = Math.Min(pos.Y, _startPoint.Y);
                var w = Math.Max(pos.X, _startPoint.X) - x;
                var h = Math.Max(pos.Y, _startPoint.Y) - y;

                InkCanvas.SetLeft(_currentEllipse, x);
                InkCanvas.SetTop(_currentEllipse, y);
                _currentEllipse.Width = w;
                _currentEllipse.Height = h;
            }
            else if (_isDrawingShape && _currentTool == EditorTool.Arrow && _currentLine != null && _currentArrowHead != null)
            {
                var pos = e.GetPosition(DrawingCanvas);
                _currentLine.X2 = pos.X;
                _currentLine.Y2 = pos.Y;
                
                double dX = pos.X - _startPoint.X;
                double dY = pos.Y - _startPoint.Y;
                double length = Math.Sqrt(dX * dX + dY * dY);
                if (length > 0)
                {
                    double angle = Math.Atan2(dY, dX);
                    double arrowLength = 20;
                    double arrowAngle = Math.PI / 6;

                    Point p1 = new Point(pos.X, pos.Y);
                    Point p2 = new Point(pos.X - arrowLength * Math.Cos(angle - arrowAngle), pos.Y - arrowLength * Math.Sin(angle - arrowAngle));
                    Point p3 = new Point(pos.X - arrowLength * Math.Cos(angle + arrowAngle), pos.Y - arrowLength * Math.Sin(angle + arrowAngle));

                    _currentArrowHead.Points.Clear();
                    _currentArrowHead.Points.Add(p1);
                    _currentArrowHead.Points.Add(p2);
                    _currentArrowHead.Points.Add(p3);
                }
            }
        }

        private void DrawingCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDrawingShape)
            {
                _isDrawingShape = false;
                if (_currentRect != null) {
                    _undoRedoManager.AddAction(new ShapeAction(DrawingCanvas, _currentRect));
                    _currentRect = null;
                }
                if (_currentEllipse != null) {
                    _undoRedoManager.AddAction(new ShapeAction(DrawingCanvas, _currentEllipse));
                    _currentEllipse = null;
                }
                if (_currentLine != null && _currentArrowHead != null) {
                    _undoRedoManager.AddAction(new ShapeAction(DrawingCanvas, _currentLine, _currentArrowHead));
                    _currentLine = null;
                    _currentArrowHead = null;
                }
                DrawingCanvas.ReleaseMouseCapture();
            }
        }
    }

    public interface IUndoableAction
    {
        void Undo();
        void Redo();
    }

    public class InkStrokeAction : IUndoableAction
    {
        private InkCanvas _canvas;
        private System.Windows.Ink.Stroke _stroke;

        public InkStrokeAction(InkCanvas canvas, System.Windows.Ink.Stroke stroke)
        {
            _canvas = canvas;
            _stroke = stroke;
        }

        public void Undo()
        {
            if (_canvas.Strokes.Contains(_stroke))
                _canvas.Strokes.Remove(_stroke);
        }

        public void Redo()
        {
            if (!_canvas.Strokes.Contains(_stroke))
                _canvas.Strokes.Add(_stroke);
        }
    }

    public class ShapeAction : IUndoableAction
    {
        private InkCanvas _canvas;
        private List<UIElement> _elements;

        public ShapeAction(InkCanvas canvas, params UIElement[] elements)
        {
            _canvas = canvas;
            _elements = new List<UIElement>(elements);
        }

        public void Undo()
        {
            foreach (var el in _elements)
            {
                if (_canvas.Children.Contains(el))
                    _canvas.Children.Remove(el);
            }
        }

        public void Redo()
        {
            foreach (var el in _elements)
            {
                if (!_canvas.Children.Contains(el))
                    _canvas.Children.Add(el);
            }
        }
    }

    public class UndoRedoManager
    {
        private Stack<IUndoableAction> _undoStack = new Stack<IUndoableAction>();
        private Stack<IUndoableAction> _redoStack = new Stack<IUndoableAction>();
        public event Action StateChanged;

        public bool CanUndo { get { return _undoStack.Count > 0; } }
        public bool CanRedo { get { return _redoStack.Count > 0; } }

        public void AddAction(IUndoableAction action)
        {
            _undoStack.Push(action);
            _redoStack.Clear();
            if (StateChanged != null) StateChanged();
        }

        public void Undo()
        {
            if (CanUndo)
            {
                var action = _undoStack.Pop();
                action.Undo();
                _redoStack.Push(action);
                if (StateChanged != null) StateChanged();
            }
        }

        public void Redo()
        {
            if (CanRedo)
            {
                var action = _redoStack.Pop();
                action.Redo();
                _undoStack.Push(action);
                if (StateChanged != null) StateChanged();
            }
        }
    }
}
