using System.Text;

namespace Optimizer.Main.Process;

internal static class TaskLauncher
{
    public static void Start(string verb, string path)
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrEmpty(executable))
        {
            Report.Error(Loc.T("dialog.fileAnalyze.launchFailed"));
            return;
        }

        string? selection = null;
        try
        {
            var directory = Path.Combine(Path.GetTempPath(), Branding.Name);
            Directory.CreateDirectory(directory);
            selection = Path.Combine(directory, "sel-" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(selection, path + "\r\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = AppContext.BaseDirectory,
                ArgumentList = { "--task", verb, "--input", selection },
                UseShellExecute = false,
            });
        }
        catch (Exception ex)
        {
            if (selection is not null)
            {
                try { File.Delete(selection); } catch (IOException) {}
            }
            Report.Error(ex.Message);
        }
    }
}
