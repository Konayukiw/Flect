namespace Flect.Decompile;

internal sealed class Decompile(Request request) : Flect.Dispatch.Batch(request)
{
    public override string Title => Loc.T("menu.jar.decompile");

    protected override async Task ProcessAsync(string path, ITaskProgress progress)
    {
        var root = Preferences.Current.Code.Decompile.OutputPath.Trim();
        var destination = root.Length > 0
            ? Flect.Files.OutputName.DeriveDirectory(
                Path.Combine(root, Path.GetFileNameWithoutExtension(path)))
            : Flect.Files.OutputName.DeriveDirectory(path);
        Directory.CreateDirectory(destination);

        progress.Status(Loc.F("msg.decompiling", Path.GetFileName(path)));

        bool finished = false;
        try
        {
            await Flect.External.ChildProcess.RunOrThrowAsync(Tools.Java,
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
                catch (Exception) { /* ignore */ }
            }
        }
    }
}
