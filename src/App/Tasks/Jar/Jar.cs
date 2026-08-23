
using Optimizer.Main.Process;

namespace Optimizer.Tasks.Jar;

internal sealed class JarDecompile(Request request) : BatchTask(request)
{
    public override string Title => Loc.T("menu.jar.decompile");

    protected override async Task ProcessAsync(string path, ITaskProgress progress)
    {
        var root = Settings.Current.Code.Decompile.OutputPath.Trim();
        var destination = root.Length > 0
            ? OutputPath.DeriveDirectory(
                Path.Combine(root, Path.GetFileNameWithoutExtension(path)))
            : OutputPath.DeriveDirectory(path);
        Directory.CreateDirectory(destination);

        progress.Status(Loc.F("msg.decompiling", Path.GetFileName(path)));

        bool finished = false;
        try
        {
            await ProcessRunner.RunOrThrowAsync(Tools.Java,
            [
                "-jar", Tools.CfrJar,
                path,
                "--outputdir", destination,
            ], progress.Token);

            if (!Directory.EnumerateFileSystemEntries(destination).Any())
            {
                throw new InvalidOperationException(Loc.T("msg.decompileEmpty"));
            }
            finished = true;
        }
        finally
        {
            if (!finished)
            {
                try { Directory.Delete(destination, recursive: true); }
                catch (Exception) {}
            }
        }
    }
}
