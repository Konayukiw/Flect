using System.Text;
using System.Windows;

namespace Flect;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var settings = Preferences.Current;
        Loc.Use(settings.General.Language);
        Theme.Apply(this, settings.General.Theme);
        Flect.Settings.Icon.Register();
        Flect.Update.Release.SyncStartup(settings.General.AutoUpdate);

        Dispatcher.UnhandledException += (_, args) =>
        {
            Flect.Display.Notice.Error(args.Exception.Message);
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

            if (args.Any(a => string.Equals(a, "--check-update", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(a, "--update", StringComparison.OrdinalIgnoreCase)))
            {
                await Flect.Update.Release.RunBackgroundUpdateAsync();
                return;
            }

            if (args.Any(a => string.Equals(a, "--preview-register", StringComparison.OrdinalIgnoreCase)))
            {
                Flect.ExplorerPreview.PreviewAssociations.Apply(Preferences.Current.Preview);
                return;
            }

            if (args.Any(a => string.Equals(a, "--preview-unregister", StringComparison.OrdinalIgnoreCase)))
            {
                Flect.ExplorerPreview.PreviewAssociations.Revert();
                return;
            }

            var request = Request.Parse(args);
            if (request is null)
            {
                Flect.Display.Notice.Error(Loc.F("msg.unknownCommand", string.Join(' ', args)));
                Shutdown();
                return;
            }

            await Flect.Dispatch.Session.RunAsync(request);
        }
        catch (Exception ex)
        {
            Flect.Display.Notice.Error(ex.Message);
        }
        finally
        {
            if (Windows.Count == 0) Shutdown();
            else ShutdownMode = ShutdownMode.OnLastWindowClose;
        }
    }
}
