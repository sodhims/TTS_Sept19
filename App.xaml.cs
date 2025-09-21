using System;
using System.Windows;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        this.DispatcherUnhandledException += (s, args) =>
        {
            MessageBox.Show(args.Exception.ToString(), "Unhandled");
            args.Handled = false; // let debugger break if attached
        };

        AppDomain.CurrentDomain.FirstChanceException += (s, args2) =>
        {
            System.Diagnostics.Debug.WriteLine(args2.Exception);
        };

        base.OnStartup(e);
    }
}
