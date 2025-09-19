using System;
using System.Windows;

namespace TTS1.WPF
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Set up global exception handling
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, 
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{e.Exception.Message}", 
                "Error", 
                MessageBoxButton.OK, 
                MessageBoxImage.Error);
            
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, 
            UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            MessageBox.Show(
                $"A fatal error occurred:\n\n{ex?.Message ?? "Unknown error"}", 
                "Fatal Error", 
                MessageBoxButton.OK, 
                MessageBoxImage.Error);
        }
    }
}