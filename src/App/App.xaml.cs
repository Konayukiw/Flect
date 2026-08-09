using System.Text;
using System.Windows;
using Optimizer.Main;

namespace Optimizer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Theme.Apply(this);
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
            var request = TaskRequest.Parse(args);
            if (request is null)
            {
                Report.Info(
                    $"{Branding.Name} runs from the Explorer context menu.\n\n" +
                    "Usage: --task <verb> --input <selection-file>");
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
