using System;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace StepCodeUsage
{
    public partial class App : System.Windows.Application
    {
        private System.Windows.Forms.NotifyIcon _notifyIcon;
        private MainWindow _mainWindow;

        private static Mutex _mutex;
        private static EventWaitHandle _showWindowEvent;
        private const string MutexName = "StepCodeUsage_SingleInstance_Mutex";
        private const string EventName = "StepCodeUsage_ShowWindow_Event";

        protected override void OnStartup(StartupEventArgs e)
        {
            _mutex = new Mutex(true, MutexName, out bool createdNew);

            if (!createdNew)
            {
                // 已有实例，发送唤醒信号
                try
                {
                    _showWindowEvent = EventWaitHandle.OpenExisting(EventName);
                    _showWindowEvent.Set();
                }
                catch { }
                Shutdown();
                return;
            }

            base.OnStartup(e);

            // 监听唤醒信号
            _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
            ThreadPool.RegisterWaitForSingleObject(_showWindowEvent,
                (state, timedOut) => Current.Dispatcher.Invoke(() => ShowMainWindow()),
                null, Timeout.Infinite, false);

            // 初始化主窗口（注意：这里不调用 Show()，使其隐藏在托盘）
            _mainWindow = new MainWindow();

            // 初始化托盘
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            try
            {
                _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
            }
            catch
            {
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            }

            _notifyIcon.Text = "模型额度监控";
            _notifyIcon.Visible = true;
            _notifyIcon.DoubleClick += (s, args) => ShowMainWindow();


            // 初始化右键菜单
            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            contextMenu.Items.Add("查看面板", null, (s, args) => ShowMainWindow());
            contextMenu.Items.Add("立即刷新", null, async (s, args) => await _mainWindow.ViewModel.UpdateUsageAsync());
            contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            // 开机自启动菜单项
            var startupItem = new System.Windows.Forms.ToolStripMenuItem("开机自启动")
            {
                CheckOnClick = true,
                Checked = StartupManager.IsStartupEnabled()
            };
            startupItem.CheckedChanged += (s, args) =>
            {
                StartupManager.SetStartup(startupItem.Checked);
            };
            contextMenu.Items.Add(startupItem);

            contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            contextMenu.Items.Add("退出", null, (s, args) => ExitApplication());

            _notifyIcon.ContextMenuStrip = contextMenu;

            ShowMainWindow();
        }

        public void ShowMainWindow()
        {
            if (_mainWindow == null || !_mainWindow.IsLoaded)
            {
                _mainWindow = new MainWindow();
            }

            _mainWindow.Show();
            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }
            _mainWindow.Activate();

            // 强制置顶，解决被其他窗口挡住的问题
            var handle = new System.Windows.Interop.WindowInteropHelper(_mainWindow).Handle;
            NativeMethods.SetForegroundWindow(handle);
        }

        public void ExitApplication()
        {
            if (_notifyIcon != null) { _notifyIcon.Visible = false; _notifyIcon.Dispose(); }
            if (_mutex != null) { _mutex.ReleaseMutex(); _mutex.Dispose(); }
            if (_showWindowEvent != null) { _showWindowEvent.Dispose(); }
            Shutdown();
        }
    }

    internal static class NativeMethods
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}