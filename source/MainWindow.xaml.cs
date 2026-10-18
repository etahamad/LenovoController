using LenovoController.Features;
using LenovoController.Providers;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using Application = System.Windows.Application;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using Control = System.Windows.Controls.Control;
using MouseButtons = System.Windows.Forms.MouseButtons;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using RadioButton = System.Windows.Controls.RadioButton;
using ToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;
using ToolStripSeparator = System.Windows.Forms.ToolStripSeparator;

namespace LenovoController
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly RadioButton[] _alwaysOnUsbButtons;
        private readonly AlwaysOnUsbFeature _alwaysOnUsbFeature = new AlwaysOnUsbFeature();
        private readonly RadioButton[] _batteryButtons;
        private readonly BatteryFeature _batteryFeature = new BatteryFeature();
        private readonly RadioButton[] _powerModeButtons;
        private readonly PowerModeFeature _powerModeFeature = new PowerModeFeature();
        private readonly FnLockFeature _fnLockFeature = new FnLockFeature();
        private readonly OverDriveFeature _overDriveFeature = new OverDriveFeature();
        private readonly TouchpadLockFeature _touchpadLockFeature = new TouchpadLockFeature();

        private readonly NotifyIcon _notifyIcon;
        private bool _isExiting;
        private bool _isRefreshing;

        public MainWindow()
        {
            InitializeComponent();

            mainWindow.Title += $" v{AssemblyName.GetAssemblyName(Assembly.GetExecutingAssembly().Location).Version}";
            _powerModeButtons = new[] { radioQuiet, radioBalance, radioPerformance };
            _batteryButtons = new[] { radioConservation, radioNormalCharge, radioRapidCharge };
            _alwaysOnUsbButtons = new[] { radioAlwaysOnUsbOff, radioAlwaysOnUsbOnWhenSleeping, radioAlwaysOnUsbOnAlways };

            // Initialize System Tray Icon & Context Menu
            _notifyIcon = new NotifyIcon
            {
                Icon = GetAppIcon(),
                Visible = true,
                Text = "Lenovo Controller"
            };
            _notifyIcon.MouseClick += NotifyIcon_MouseClick;
            _notifyIcon.ContextMenuStrip = CreateContextMenu();

            Refresh();
            LoadSettings();
        }

        private static System.Drawing.Icon GetAppIcon()
        {
            try
            {
                var exePath = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    var extracted = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                    if (extracted != null)
                        return extracted;
                }

                var dirIcon = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                if (File.Exists(dirIcon))
                    return new System.Drawing.Icon(dirIcon);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Could not load icon: " + ex);
            }

            return System.Drawing.SystemIcons.Application;
        }

        private ContextMenuStrip CreateContextMenu()
        {
            var menu = new ContextMenuStrip();

            var itemOpen = new ToolStripMenuItem("Open Lenovo Controller")
            {
                Font = new Font(menu.Font, System.Drawing.FontStyle.Bold)
            };
            itemOpen.Click += (s, e) => ShowMainWindow();
            menu.Items.Add(itemOpen);

            menu.Items.Add(new ToolStripSeparator());

            // Power Mode submenu
            var itemPowerMode = new ToolStripMenuItem("Power Mode");
            var itemQuiet = new ToolStripMenuItem("Quiet", null, (s, e) => SetPowerModeFromTray(PowerModeState.Quiet));
            var itemBalance = new ToolStripMenuItem("Balance", null, (s, e) => SetPowerModeFromTray(PowerModeState.Balance));
            var itemPerformance = new ToolStripMenuItem("Performance", null, (s, e) => SetPowerModeFromTray(PowerModeState.Performance));
            itemPowerMode.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { itemQuiet, itemBalance, itemPerformance });
            menu.Items.Add(itemPowerMode);

            // Battery Mode submenu
            var itemBatteryMode = new ToolStripMenuItem("Battery Mode");
            var itemConservation = new ToolStripMenuItem("Conservation", null, (s, e) => SetBatteryModeFromTray(BatteryState.Conservation));
            var itemNormal = new ToolStripMenuItem("Normal", null, (s, e) => SetBatteryModeFromTray(BatteryState.Normal));
            var itemRapid = new ToolStripMenuItem("Rapid Charge", null, (s, e) => SetBatteryModeFromTray(BatteryState.RapidCharge));
            itemBatteryMode.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { itemConservation, itemNormal, itemRapid });
            menu.Items.Add(itemBatteryMode);

            menu.Items.Add(new ToolStripSeparator());

            // Options
            var itemStartWithWindows = new ToolStripMenuItem("Start with Windows") { CheckOnClick = true };
            itemStartWithWindows.Click += (s, e) =>
            {
                var isChecked = itemStartWithWindows.Checked;
                chkStartWithWindows.IsChecked = isChecked;
                TaskSchedulerProvider.SetTaskScheduled(isChecked, AppConfig.StartMinimized);
            };

            var itemStartMinimized = new ToolStripMenuItem("Start Minimized") { CheckOnClick = true };
            itemStartMinimized.Click += (s, e) =>
            {
                var isChecked = itemStartMinimized.Checked;
                chkStartMinimized.IsChecked = isChecked;
                AppConfig.StartMinimized = isChecked;
                if (chkStartWithWindows.IsChecked == true)
                    TaskSchedulerProvider.SetTaskScheduled(true, isChecked);
            };

            var itemMinimizeOnClose = new ToolStripMenuItem("Minimize on Close") { CheckOnClick = true };
            itemMinimizeOnClose.Click += (s, e) =>
            {
                var isChecked = itemMinimizeOnClose.Checked;
                chkMinimizeOnClose.IsChecked = isChecked;
                AppConfig.MinimizeToTrayOnClose = isChecked;
            };

            menu.Items.Add(itemStartWithWindows);
            menu.Items.Add(itemStartMinimized);
            menu.Items.Add(itemMinimizeOnClose);

            menu.Items.Add(new ToolStripSeparator());

            var itemExit = new ToolStripMenuItem("Exit", null, (s, e) => ExitApplication());
            menu.Items.Add(itemExit);

            menu.Opening += (s, e) =>
            {
                try
                {
                    var currentPower = _powerModeFeature.GetState();
                    itemQuiet.Checked = currentPower == PowerModeState.Quiet;
                    itemBalance.Checked = currentPower == PowerModeState.Balance;
                    itemPerformance.Checked = currentPower == PowerModeState.Performance;

                    var currentBattery = _batteryFeature.GetState();
                    itemConservation.Checked = currentBattery == BatteryState.Conservation;
                    itemNormal.Checked = currentBattery == BatteryState.Normal;
                    itemRapid.Checked = currentBattery == BatteryState.RapidCharge;

                    itemStartWithWindows.Checked = TaskSchedulerProvider.IsTaskScheduled();
                    itemStartMinimized.Checked = AppConfig.StartMinimized;
                    itemMinimizeOnClose.Checked = AppConfig.MinimizeToTrayOnClose;
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning("Failed to update context menu checks: " + ex);
                }
            };

            return menu;
        }

        private void SetPowerModeFromTray(PowerModeState state)
        {
            try
            {
                _powerModeFeature.SetState(state);
                _powerModeButtons[(int)state].IsChecked = true;
            }
            catch (Exception ex)
            {
                Trace.TraceError("Failed to set power mode from tray: " + ex);
            }
        }

        private void SetBatteryModeFromTray(BatteryState state)
        {
            try
            {
                _batteryFeature.SetState(state);
                _batteryButtons[(int)state].IsChecked = true;
            }
            catch (Exception ex)
            {
                Trace.TraceError("Failed to set battery mode from tray: " + ex);
            }
        }

        private void NotifyIcon_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ShowMainWindow();
            }
        }

        public void ShowMainWindow()
        {
            Show();
            ShowInTaskbar = true;
            Visibility = Visibility.Visible;
            WindowState = WindowState.Normal;
            Activate();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                ShowInTaskbar = false;
            }
            base.OnStateChanged(e);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (AppConfig.MinimizeToTrayOnClose && !_isExiting)
            {
                e.Cancel = true;
                Hide();
                ShowInTaskbar = false;
                return;
            }

            _notifyIcon?.Dispose();
            base.OnClosing(e);
        }

        private void ExitApplication()
        {
            _isExiting = true;
            _notifyIcon?.Dispose();
            Application.Current.Shutdown();
        }

        private void LoadSettings()
        {
            _isRefreshing = true;
            try
            {
                chkStartWithWindows.IsChecked = TaskSchedulerProvider.IsTaskScheduled();
                chkStartMinimized.IsChecked = AppConfig.StartMinimized;
                chkMinimizeOnClose.IsChecked = AppConfig.MinimizeToTrayOnClose;
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private class FeatureCheck
        {
            private readonly Action _check;
            private readonly Action _disable;

            internal FeatureCheck(Action check, Action disable)
            {
                _check = check;
                _disable = disable;
            }

            internal void Check() => _check();
            internal void Disable() => _disable();
        }

        private void Refresh()
        {
            _isRefreshing = true;
            try
            {
                var features = new[]
                {
                    new FeatureCheck(
                        () => _powerModeButtons[(int) _powerModeFeature.GetState()].IsChecked = true,
                        () => DisableControls(_powerModeButtons)),
                    new FeatureCheck(
                        () => _batteryButtons[(int) _batteryFeature.GetState()].IsChecked = true,
                        () => DisableControls(_batteryButtons)),
                    new FeatureCheck(
                        () => _alwaysOnUsbButtons[(int) _alwaysOnUsbFeature.GetState()].IsChecked = true,
                        () => DisableControls(_alwaysOnUsbButtons)),
                    new FeatureCheck(
                        () => chkOverDrive.IsChecked = _overDriveFeature.GetState() == OverDriveState.On,
                        () => chkOverDrive.IsEnabled = false),
                    new FeatureCheck(
                        () => chkTouchpadLock.IsChecked = _touchpadLockFeature.GetState() == TouchpadLockState.On,
                        () => chkTouchpadLock.IsEnabled = false),
                    new FeatureCheck(
                        () => chkFnLock.IsChecked = _fnLockFeature.GetState() == FnLockState.On,
                        () => chkFnLock.IsEnabled = false)
                };

                foreach (var feature in features)
                {
                    try
                    {
                        feature.Check();
                    }
                    catch (Exception e)
                    {
                        Trace.TraceInformation("Could not refresh feature: " + e);
                        feature.Disable();
                    }
                }

                UpdateBatteryInfo();
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private void UpdateBatteryInfo()
        {
            try
            {
                var power = SystemInformation.PowerStatus;
                var percent = (int)(power.BatteryLifePercent * 100);
                lblBatteryLevel.Text = $"Level: {percent}%";

                var status = "Unknown";
                if (power.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online)
                {
                    if (power.BatteryChargeStatus.HasFlag(BatteryChargeStatus.Charging))
                        status = "Plugged in (Charging)";
                    else if (percent >= 99)
                        status = "Plugged in (Full)";
                    else
                        status = "Plugged in (Not Charging)";
                }
                else
                {
                    status = "On Battery (Discharging)";
                }

                lblBatteryStatus.Text = $"Power: {status}";
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Failed to update battery info: " + ex);
                lblBatteryLevel.Text = "Level: --";
                lblBatteryStatus.Text = "Power: --";
            }
        }

        private void DisableControls(Control[] buttons)
        {
            foreach (var btn in buttons)
                btn.IsEnabled = false;
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        private void radioPowerMode_Checked(object sender, RoutedEventArgs e)
        {
            if (_isRefreshing) return;
            _powerModeFeature.SetState((PowerModeState)Array.IndexOf(_powerModeButtons, sender));
        }

        private void radioBattery_Checked(object sender, RoutedEventArgs e)
        {
            if (_isRefreshing) return;
            _batteryFeature.SetState((BatteryState)Array.IndexOf(_batteryButtons, sender));
        }

        private void radioAlwaysOnUsb_Checked(object sender, RoutedEventArgs e)
        {
            if (_isRefreshing) return;
            _alwaysOnUsbFeature.SetState((AlwaysOnUsbState)Array.IndexOf(_alwaysOnUsbButtons, sender));
        }

        private void chkOverDrive_Checked(object sender, RoutedEventArgs e)
        {
            if (_isRefreshing) return;
            var state = chkOverDrive.IsChecked.GetValueOrDefault(false)
                ? OverDriveState.On
                : OverDriveState.Off;
            _overDriveFeature.SetState(state);
        }

        private void chkTouchpadLock_Checked(object sender, RoutedEventArgs e)
        {
            if (_isRefreshing) return;
            var state = chkTouchpadLock.IsChecked.GetValueOrDefault(false)
                ? TouchpadLockState.On
                : TouchpadLockState.Off;
            _touchpadLockFeature.SetState(state);
        }

        private void chkFnLock_Checked(object sender, RoutedEventArgs e)
        {
            if (_isRefreshing) return;
            var state = chkFnLock.IsChecked.GetValueOrDefault(false)
                ? FnLockState.On
                : FnLockState.Off;
            _fnLockFeature.SetState(state);
        }

        private void chkStartWithWindows_Checked(object sender, RoutedEventArgs e)
        {
            if (_isRefreshing) return;
            var enabled = chkStartWithWindows.IsChecked == true;
            TaskSchedulerProvider.SetTaskScheduled(enabled, chkStartMinimized.IsChecked == true);
        }

        private void chkStartMinimized_Checked(object sender, RoutedEventArgs e)
        {
            if (_isRefreshing) return;
            var enabled = chkStartMinimized.IsChecked == true;
            AppConfig.StartMinimized = enabled;
            if (chkStartWithWindows.IsChecked == true)
            {
                TaskSchedulerProvider.SetTaskScheduled(true, enabled);
            }
        }

        private void chkMinimizeOnClose_Checked(object sender, RoutedEventArgs e)
        {
            if (_isRefreshing) return;
            AppConfig.MinimizeToTrayOnClose = chkMinimizeOnClose.IsChecked == true;
        }
    }
}