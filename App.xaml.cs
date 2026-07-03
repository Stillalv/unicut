using System.Windows;
using System.Drawing;
using System.Windows.Forms;
using Application = System.Windows.Application;
using UNICUT.Core;

namespace UNICUT
{
    public partial class App : Application
    {
        private NotifyIcon _notifyIcon;
        private HotkeyService _hotkeyService;
        private OverlayWindow _overlayWindow;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Set shutdown mode to allow background execution
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Initialize NotifyIcon (System Tray)
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = SystemIcons.Application;
            _notifyIcon.Visible = true;
            _notifyIcon.Text = "UNICUT (Ctrl+Shift+S)";
            
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Exit", null, (s, args) => ExitApplication());
            _notifyIcon.ContextMenuStrip = contextMenu;

            // Initialize HotkeyService
            _hotkeyService = new HotkeyService();
            _hotkeyService.HotkeyPressed += OnHotkeyPressed;
            _hotkeyService.RegisterHotkey();
        }

        private void OnHotkeyPressed()
        {
            if (_overlayWindow == null || !_overlayWindow.IsLoaded)
            {
                _overlayWindow = new OverlayWindow();
                _overlayWindow.Show();
            }
        }

        private void ExitApplication()
        {
            _hotkeyService?.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            Shutdown();
        }
    }
}
