using System.Windows;

namespace UsualAudioConverter;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (s, args) =>
        {
            MessageBox.Show(
                $"起動時エラーが発生しました:\n\n{args.Exception}",
                "エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            MessageBox.Show(
                $"重大なエラーが発生しました:\n\n{args.ExceptionObject}",
                "致命的エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        };
    }
}
