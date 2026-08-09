using Optimizer.Main;

namespace Optimizer.Tasks;

internal sealed class JarDecompile(TaskRequest request) : BatchTask(request)
{
    public override string Title => "Decompile";

    protected override async Task ProcessAsync(string path, ITaskProgress progress)
    {
        var destination = OutputPath.DeriveDirectory(path);
        Directory.CreateDirectory(destination);

        progress.Status($"{Path.GetFileName(path)} — decompiling");

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
                throw new InvalidOperationException("The decompiler produced no output.");
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
