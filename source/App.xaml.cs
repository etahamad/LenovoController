using LenovoController.Providers;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace LenovoController
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var errorText = e.Exception.ToString();
            Trace.TraceError(errorText);
            MessageBox.Show(errorText, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Environment.Exit(-1);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var startMinimized = e.Args.Any(a => a.Equals("-minimized", StringComparison.OrdinalIgnoreCase) ||
                                                 a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
                                                 a.Equals("/minimized", StringComparison.OrdinalIgnoreCase));

            var mainWindow = new MainWindow();

            if (startMinimized)
            {
                mainWindow.WindowState = WindowState.Minimized;
                mainWindow.ShowInTaskbar = false;
                mainWindow.Visibility = Visibility.Hidden;
            }
            else
            {
                mainWindow.Show();
            }
        }
    }
}