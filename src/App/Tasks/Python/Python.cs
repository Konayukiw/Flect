using System.Text;

using Optimizer.Main.Process;

namespace Optimizer.Tasks.Python;

internal sealed class PythonObfuscate(Request request) : BatchTask(request)
{
    public override string Title => Loc.T("menu.python.obfuscate");

    protected override async Task ProcessAsync(string path, ITaskProgress progress)
    {
        var settings = Settings.Current.Code.Obfuscate;
        var output = OutputPath.Derive(path, "_obf", ".py");

        progress.Status(Loc.F("msg.obfuscating", Path.GetFileName(path)));

        string executable;
        string[] prefix;
        try
        {
            (executable, prefix) = Tools.Python;
        }
        catch (FileNotFoundException)
        {
            throw new InvalidOperationException(Loc.T("msg.obfNoPython"));
        }

        var config = Path.Combine(OutputPath.TempDirectory(), $"pyobf-{Guid.NewGuid():N}.ini");        try
        {
            await File.WriteAllTextAsync(config, BuildIni(settings), progress.Token);
            await ProcessRunner.RunOrThrowAsync(executable,
            [
                .. prefix,
                "-m", "pyobfuscate",
                path,
                output,
                "--config", config,
            ], progress.Token, workingDirectory: Tools.PyObfuscateDirectory);
        }
        finally
        {
            OutputPath.SafeDelete(config);
        }
    }

    private static string BuildIni(ObfuscateSettings s)
    {
        int nameMin = Math.Max(1, s.NameLengthMin);
        int nameMax = Math.Max(nameMin, s.NameLengthMax);
        int junk = Math.Clamp(s.JunkFrequency, 0, 10);
        int layers = Math.Max(0, s.EnvKeyLayers);
        int pool = Math.Max(2, s.ArgPoolSize);
        var seed = int.TryParse(s.Seed.Trim(), out var parsed) ? parsed.ToString() : string.Empty;

        var builder = new StringBuilder();
        builder.AppendLine("[names]");
        builder.AppendLine($"length = {nameMin}-{nameMax}");
        builder.AppendLine();
        builder.AppendLine("[junk]");
        builder.AppendLine($"frequency = {junk}");
        builder.AppendLine();
        builder.AppendLine("[string]");
        builder.AppendLine($"env_key = {Bool(s.UseEnvKey)}");
        builder.AppendLine($"env_key_path = {s.EnvKeyPath.Trim()}");
        builder.AppendLine($"xor = {Bool(s.UseXor)}");
        builder.AppendLine($"swap = {Bool(s.UseSwap)}");
        builder.AppendLine($"rotate = {Bool(s.UseRotate)}");
        builder.AppendLine($"byte_shuffle = {Bool(s.UseByteShuffle)}");
        builder.AppendLine($"base85 = {Bool(s.UseBase85)}");
        builder.AppendLine();
        builder.AppendLine("[obfuscation]");
        builder.AppendLine($"rename = {Bool(s.Rename)}");
        builder.AppendLine($"hide_imports = {Bool(s.HideImports)}");
        builder.AppendLine($"value_calc = {Bool(s.ValueCalc)}");
        builder.AppendLine($"encrypt_strings = {Bool(s.EncryptStrings)}");
        builder.AppendLine($"seed = {seed}");
        builder.AppendLine();
        builder.AppendLine("[strong_obf]");
        builder.AppendLine($"rename_classes = {Bool(s.RenameClasses)}");
        builder.AppendLine($"attr_indirect = {Bool(s.AttrIndirect)}");
        builder.AppendLine($"builtins_table = {Bool(s.BuiltinsTable)}");
        builder.AppendLine($"bool_none_expr = {Bool(s.BoolNoneExpr)}");
        builder.AppendLine($"integer_encode = {Bool(s.IntegerEncode)}");
        builder.AppendLine($"scope_arg_reuse = {Bool(s.ScopeArgReuse)}");
        builder.AppendLine($"arg_pool_size = {pool}");
        builder.AppendLine($"wrap_junk_if = {Bool(s.WrapJunkIf)}");
        builder.AppendLine($"env_key_layers = {layers}");
        return builder.ToString();
    }

    private static string Bool(bool value) => value ? "true" : "false";
}
