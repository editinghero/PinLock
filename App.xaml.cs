using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace PinLock
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                base.OnStartup(e);
                
                // Ensure single instance
                var currentProcess = Process.GetCurrentProcess();
                var processes = Process.GetProcessesByName(currentProcess.ProcessName);
                
                if (processes.Length > 1)
                {
                    MessageBox.Show("PinLock is already running. Check your system tray.", "PinLock", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    Shutdown();
                    return;
                }

                // Set up global exception handling
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                DispatcherUnhandledException += OnDispatcherUnhandledException;
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("Failed to start PinLock: {0}", ex.Message), "Startup Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var ex = e.ExceptionObject as Exception;
                var message = ex != null ? ex.Message : "Unknown error";
                MessageBox.Show(string.Format("An unexpected error occurred: {0}", message), 
                    "PinLock Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // If we can't even show the error message, just exit
            }
            finally
            {
                Shutdown();
            }
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                MessageBox.Show(string.Format("An error occurred in the user interface: {0}", e.Exception.Message), 
                    "PinLock UI Error", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true; // Prevent the application from crashing
            }
            catch
            {
                // If we can't handle the exception, let it crash
                e.Handled = false;
            }
        }
    }
}