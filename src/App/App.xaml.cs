using System.Text;
using System.Windows;

using Optimizer.Main.Process;
using Optimizer.Main.Update;

namespace Optimizer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var settings = Optimizer.Main.Config.Settings.Current;
        Loc.Use(settings.General.Language);
        Theme.Apply(this, settings.General.Theme);
        AppIcon.Register();
        Updater.SyncStartup(settings.General.AutoUpdate);

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
                Gui.Pages.SettingsGui.Open();
                return;
            }

            if (args.Any(a => string.Equals(a, "--check-update", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(a, "--update", StringComparison.OrdinalIgnoreCase)))
            {
                await Updater.RunBackgroundUpdateAsync();
                return;
            }

            var request = Request.Parse(args);
            if (request is null)
            {
                Report.Error(Loc.F("msg.unknownCommand", string.Join(' ', args)));
                Shutdown();
                return;
            }

            await Runner.RunAsync(request);
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
