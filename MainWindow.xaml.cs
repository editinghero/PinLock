using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PinLock
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<AppInfo> _apps = new ObservableCollection<AppInfo>();
        private DispatcherTimer _refreshTimer;
        private IntPtr _pinnedWindow = IntPtr.Zero;
        private IntPtr _hook = IntPtr.Zero;
        private bool _isMonitoring = false;
        private NotifyIcon _notifyIcon;
        private WinEventDelegate _winEventDelegate;

        // Win32 API declarations
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        private static extern void LockWorkStation();

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("psapi.dll")]
        private static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, System.Text.StringBuilder lpBaseName, uint nSize);

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        // Constants
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const int SW_MAXIMIZE = 3;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const int GWL_EXSTYLE = -20;
        private const uint WS_EX_TOOLWINDOW = 0x00000080;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_VM_READ = 0x0010;
        
        // DWM constants for Windows 11 effects
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_MICA_EFFECT = 1029;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        private const uint WS_EX_NOREDIRECTIONBITMAP = 0x00200000;

        // Delegates
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                InitializeApp();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format("Failed to initialize PinLock: {0}", ex.Message), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Windows.Application.Current.Shutdown();
            }
        }

        private void InitializeApp()
        {
            try
            {
                AppListView.ItemsSource = _apps;
                _winEventDelegate = WinEventProc;

                // Setup refresh timer - longer interval to prevent deselection
                _refreshTimer = new DispatcherTimer();
                _refreshTimer.Interval = TimeSpan.FromSeconds(10);
                _refreshTimer.Tick += (s, e) => SafeExecute(RefreshAppListSafely);
                _refreshTimer.Start();

                // Setup system tray
                SetupSystemTray();

                // Initial refresh
                RefreshAppList();

                // Handle window events
                this.StateChanged += MainWindow_StateChanged;
                this.SizeChanged += MainWindow_SizeChanged;
                this.Loaded += MainWindow_Loaded;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format("Failed to initialize app components: {0}", ex.Message), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SafeExecute(Action action)
        {
            try
            {
                if (action != null)
                    action.Invoke();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error in SafeExecute: {0}", ex.Message));
            }
        }

        private void SetupSystemTray()
        {
            try
            {
                _notifyIcon = new NotifyIcon();
                
                // Try to load custom logo, fallback to shield if not available
                try
                {
                    var logoStream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/logo.png"));
                    if (logoStream != null)
                    {
                        using (var bitmap = new System.Drawing.Bitmap(logoStream.Stream))
                        {
                            _notifyIcon.Icon = System.Drawing.Icon.FromHandle(bitmap.GetHicon());
                        }
                    }
                    else
                    {
                        _notifyIcon.Icon = SystemIcons.Shield;
                    }
                }
                catch
                {
                    _notifyIcon.Icon = SystemIcons.Shield;
                }
                
                _notifyIcon.Text = "PinLock - Premium App Security";
                _notifyIcon.Visible = false;

                var contextMenu = new ContextMenuStrip();
                contextMenu.Items.Add("Show PinLock", null, (s, e) => SafeExecute(() => {
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                    _notifyIcon.Visible = false;
                }));
                contextMenu.Items.Add("-");
                contextMenu.Items.Add("Exit", null, (s, e) => SafeExecute(() => {
                    StopMonitoring();
                    _notifyIcon.Visible = false;
                    System.Windows.Application.Current.Shutdown();
                }));

                _notifyIcon.ContextMenuStrip = contextMenu;
                _notifyIcon.DoubleClick += (s, e) => SafeExecute(() => {
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                    _notifyIcon.Visible = false;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Failed to setup system tray: {0}", ex.Message));
            }
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            SafeExecute(() => {
                if (WindowState == WindowState.Minimized && _notifyIcon != null)
                {
                    Hide();
                    _notifyIcon.Visible = true;
                    try
                    {
                        _notifyIcon.ShowBalloonTip(2000, "PinLock", "Application minimized to tray", ToolTipIcon.Info);
                    }
                    catch
                    {
                        // Ignore balloon tip errors
                    }
                }
            });
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SafeExecute(() => {
                if (_isMonitoring && _pinnedWindow != IntPtr.Zero)
                {
                    LockAndReset();
                }
            });
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            WindowState = WindowState.Minimized;
        }

        private void RefreshAppList()
        {
            SafeExecute(() => {
                _apps.Clear();
                try
                {
                    EnumWindows(EnumWindowCallback, IntPtr.Zero);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("Error enumerating windows: {0}", ex.Message));
                }
                
                Dispatcher.Invoke(() => {
                    StatusText.Text = string.Format("Found {0} running applications - Select one to secure", _apps.Count);
                });
            });
        }

        private void RefreshAppListSafely()
        {
            SafeExecute(() => {
                // Store current selection to restore it after refresh
                AppInfo selectedApp = null;
                IntPtr selectedHandle = IntPtr.Zero;
                
                Dispatcher.Invoke(() => {
                    selectedApp = AppListView.SelectedItem as AppInfo;
                    selectedHandle = selectedApp != null ? selectedApp.Handle : IntPtr.Zero;
                });
                
                // Refresh the list
                _apps.Clear();
                try
                {
                    EnumWindows(EnumWindowCallback, IntPtr.Zero);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("Error in safe refresh: {0}", ex.Message));
                }
                
                Dispatcher.Invoke(() => {
                    StatusText.Text = string.Format("Found {0} running applications - Select one to secure", _apps.Count);
                    
                    // Restore selection if the app still exists
                    if (selectedHandle != IntPtr.Zero)
                    {
                        for (int i = 0; i < _apps.Count; i++)
                        {
                            if (_apps[i].Handle == selectedHandle)
                            {
                                AppListView.SelectedIndex = i;
                                break;
                            }
                        }
                    }
                });
            });
        }

        private bool EnumWindowCallback(IntPtr hWnd, IntPtr lParam)
        {
            try
            {
                // Must be visible
                if (!IsWindowVisible(hWnd)) return true;

                // Skip tool windows (tray apps, tooltips, etc.)
                try
                {
                    uint exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
                    if ((exStyle & WS_EX_TOOLWINDOW) != 0) return true;
                }
                catch
                {
                    return true;
                }

                // Get window title
                string windowTitle = "";
                try
                {
                    int length = GetWindowTextLength(hWnd);
                    if (length > 0)
                    {
                        var title = new System.Text.StringBuilder(length + 1);
                        GetWindowText(hWnd, title, title.Capacity);
                        windowTitle = title.ToString().Trim();
                    }
                }
                catch
                {
                    return true;
                }

                // Get window class name for filtering
                string classNameStr = "";
                try
                {
                    var className = new System.Text.StringBuilder(256);
                    GetClassName(hWnd, className, className.Capacity);
                    classNameStr = className.ToString();
                }
                catch
                {
                    return true;
                }

                // Skip system windows and tray-related windows
                if (classNameStr == "Shell_TrayWnd" ||
                    classNameStr == "Shell_SecondaryTrayWnd" ||
                    classNameStr == "NotifyIconOverflowWindow" ||
                    classNameStr == "WorkerW" ||
                    classNameStr == "Progman" ||
                    classNameStr == "DV2ControlHost" ||
                    classNameStr.Contains("Tray") ||
                    classNameStr.Contains("Notification")) return true;

                // Skip our own app
                if (windowTitle.Contains("PinLock")) return true;

                // Get window size
                try
                {
                    RECT rect;
                    GetWindowRect(hWnd, out rect);
                    int width = rect.Right - rect.Left;
                    int height = rect.Bottom - rect.Top;
                    if (width < 50 || height < 30) return true;
                }
                catch
                {
                    return true;
                }

                uint processId;
                try
                {
                    GetWindowThreadProcessId(hWnd, out processId);
                }
                catch
                {
                    return true;
                }

                Process process = null;
                try
                {
                    process = Process.GetProcessById((int)processId);
                    string processName = process.ProcessName.ToLower();
                    
                    // Skip critical system processes
                    if (processName == "dwm" ||
                        processName == "csrss" ||
                        processName == "winlogon" ||
                        processName == "smss" ||
                        processName == "wininit" ||
                        processName == "services" ||
                        processName == "lsass" ||
                        processName == "svchost") return true;

                    // Check if we already have this window
                    bool alreadyExists = false;
                    foreach (var existingApp in _apps)
                    {
                        if (existingApp.Handle == hWnd)
                        {
                            alreadyExists = true;
                            break;
                        }
                    }
                    if (alreadyExists) return true;

                    // Use process name if no window title
                    if (string.IsNullOrWhiteSpace(windowTitle))
                    {
                        windowTitle = process.ProcessName;
                    }

                    var appInfo = new AppInfo
                    {
                        Handle = hWnd,
                        ProcessId = processId,
                        ProcessName = process.ProcessName + ".exe",
                        WindowTitle = windowTitle,
                        Icon = GetAppIcon(process)
                    };

                    Dispatcher.Invoke(() => _apps.Add(appInfo));
                }
                catch
                {
                    // Ignore errors and continue enumeration
                }
                finally
                {
                    if (process != null)
                        process.Dispose();
                }
            }
            catch
            {
                // Ignore errors and continue enumeration
            }

            return true;
        }

        private BitmapSource GetAppIcon(Process process)
        {
            try
            {
                if (process != null && process.MainModule != null && process.MainModule.FileName != null)
                {
                    var icon = System.Drawing.Icon.ExtractAssociatedIcon(process.MainModule.FileName);
                    if (icon != null)
                    {
                        return Imaging.CreateBitmapSourceFromHIcon(
                            icon.Handle,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                    }
                }
            }
            catch
            {
                // Ignore icon extraction errors
            }
            return null;
        }

        private void AppListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            PinButton.IsEnabled = AppListView.SelectedItem != null;
        }

        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            SafeExecute(() => {
                var selectedApp = AppListView.SelectedItem as AppInfo;
                if (selectedApp != null)
                {
                    StopMonitoring();
                    _pinnedWindow = selectedApp.Handle;

                    try
                    {
                        // Maximize and set topmost
                        ShowWindow(_pinnedWindow, SW_MAXIMIZE);
                        SetForegroundWindow(_pinnedWindow);
                        SetWindowPos(_pinnedWindow, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

                        StartMonitoring();

                        StatusText.Text = string.Format("🔒 SECURED: {0} - Window is now protected. Any other action will lock Windows!", selectedApp.ProcessName);
                        PinButton.Content = "✅ App Secured";
                        PinButton.IsEnabled = false;
                    }
                    catch (Exception ex)
                    {
                        StatusText.Text = string.Format("Failed to secure application: {0}", ex.Message);
                        StopMonitoring();
                    }
                }
            });
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            SafeExecute(RefreshAppList);
        }

        private void AddCustomButton_Click(object sender, RoutedEventArgs e)
        {
            SafeExecute(() => {
                var dialog = new Microsoft.Win32.OpenFileDialog();
                dialog.Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*";
                dialog.Title = "Select Application to Add";
                
                if (dialog.ShowDialog() == true)
                {
                    try
                    {
                        var process = Process.Start(dialog.FileName);
                        if (process != null)
                        {
                            // Wait a moment for the process to start and create its window
                            System.Threading.Thread.Sleep(2000);
                            
                            // Refresh the app list to include the newly started app
                            RefreshAppList();
                            
                            StatusText.Text = string.Format("Started {0} - It should now appear in the list", System.IO.Path.GetFileName(dialog.FileName));
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show(string.Format("Failed to start application: {0}", ex.Message), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            });
        }

        private void StartMonitoring()
        {
            SafeExecute(() => {
                if (_isMonitoring || _pinnedWindow == IntPtr.Zero) return;

                try
                {
                    _hook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
                        IntPtr.Zero, _winEventDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);

                    if (_hook != IntPtr.Zero)
                        _isMonitoring = true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("Failed to start monitoring: {0}", ex.Message));
                }
            });
        }

        private void StopMonitoring()
        {
            SafeExecute(() => {
                try
                {
                    if (_hook != IntPtr.Zero)
                    {
                        UnhookWinEvent(_hook);
                        _hook = IntPtr.Zero;
                    }

                    if (_pinnedWindow != IntPtr.Zero)
                    {
                        try
                        {
                            SetWindowPos(_pinnedWindow, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
                        }
                        catch
                        {
                            // Ignore errors when removing topmost
                        }
                        _pinnedWindow = IntPtr.Zero;
                    }

                    _isMonitoring = false;
                    
                    Dispatcher.Invoke(() => {
                        PinButton.Content = "🔒 Pin Selected App";
                        PinButton.IsEnabled = AppListView.SelectedItem != null;
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("Error stopping monitoring: {0}", ex.Message));
                }
            });
        }

        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            SafeExecute(() => {
                if (eventType == EVENT_SYSTEM_FOREGROUND && hwnd != IntPtr.Zero && _pinnedWindow != IntPtr.Zero)
                {
                    try
                    {
                        uint newProcessId;
                        uint pinnedProcessId;
                        GetWindowThreadProcessId(hwnd, out newProcessId);
                        GetWindowThreadProcessId(_pinnedWindow, out pinnedProcessId);

                        if (newProcessId != pinnedProcessId)
                        {
                            Dispatcher.Invoke(() => LockAndReset());
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(string.Format("Error in WinEventProc: {0}", ex.Message));
                    }
                }
            });
        }

        private void LockAndReset()
        {
            SafeExecute(() => {
                StopMonitoring();
                
                try
                {
                    LockWorkStation();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("Failed to lock workstation: {0}", ex.Message));
                }

                var timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(1);
                timer.Tick += (s, e) => {
                    timer.Stop();
                    RefreshAppList();
                    StatusText.Text = "🔒 Windows locked - Security restored. Select a new application to secure.";
                };
                timer.Start();
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            SafeExecute(() => {
                StopMonitoring();
                if (_refreshTimer != null)
                    _refreshTimer.Stop();
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }
            });
            base.OnClosed(e);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SafeExecute(() => {
                ApplyWindowsEffects();
            });
        }

        private void ApplyWindowsEffects()
        {
            try
            {
                var windowHelper = new WindowInteropHelper(this);
                var hwnd = windowHelper.Handle;

                if (hwnd == IntPtr.Zero)
                    return;

                // Check Windows version
                var version = Environment.OSVersion.Version;
                bool isWindows11 = version.Major >= 10 && version.Build >= 22000;

                if (isWindows11)
                {
                    // Try to apply Mica effect first (Windows 11 22H2+)
                    int micaEffect = 2; // DWMSBT_MAINWINDOW
                    int result = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref micaEffect, sizeof(int));
                    
                    if (result != 0)
                    {
                        // Fallback to older Mica attribute
                        int useMica = 1;
                        DwmSetWindowAttribute(hwnd, DWMWA_MICA_EFFECT, ref useMica, sizeof(int));
                    }
                }
                else
                {
                    // Windows 10 - Apply Acrylic effect by extending frame
                    var margins = new MARGINS
                    {
                        cxLeftWidth = -1,
                        cxRightWidth = -1,
                        cyTopHeight = -1,
                        cyBottomHeight = -1
                    };
                    DwmExtendFrameIntoClientArea(hwnd, ref margins);
                }

                // Enable dark mode
                int useDarkMode = 1;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));

                Debug.WriteLine(string.Format("Applied Windows effects. Windows 11: {0}", isWindows11));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Failed to apply Windows effects: {0}", ex.Message));
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SafeExecute(() => {
                if (e.ClickCount == 2)
                {
                    // Double-click to maximize/restore
                    WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                }
                else
                {
                    // Single-click to drag
                    DragMove();
                }
            });
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            SafeExecute(() => {
                WindowState = WindowState.Minimized;
            });
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            SafeExecute(() => {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            });
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            SafeExecute(() => {
                Close();
            });
        }
    }

    public class AppInfo
    {
        public IntPtr Handle { get; set; }
        public uint ProcessId { get; set; }
        public string ProcessName { get; set; }
        public string WindowTitle { get; set; }
        public BitmapSource Icon { get; set; }
    }
}