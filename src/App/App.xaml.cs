using System.Text;
using System.Windows;
using Optimizer.Gui;
using Optimizer.Main;

namespace Optimizer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var settings = Settings.Current;
        Loc.Use(settings.General.Language);
        Theme.Apply(this, settings.General.Theme);
        AppIcon.Register();

        Dispatcher.UnhandledException += (_, args) =>
        {
            Report.Error(args.Exception.Message);
            args.Handled = true;
            Shutdown(1);
        };

        _ = StartAsync(e.Args);
    }

    private async Task StartAsync(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                SettingsWindow.Open();
                return;
            }

            var request = TaskRequest.Parse(args);
            if (request is null)
            {
                Report.Error(Loc.F("msg.unknownCommand", string.Join(' ', args)));
                Shutdown();
                return;
            }

            await TaskRunner.RunAsync(request);
        }
        catch (Exception ex)
        {
            Report.Error(ex.Message);
        }
        finally
        {
            if (Windows.Count == 0) Shutdown();
            else ShutdownMode = ShutdownMode.OnLastWindowClose;
        }
    }
}
