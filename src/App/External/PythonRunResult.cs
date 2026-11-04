namespace Flect.External;

internal sealed record PythonRunResult(int ExitCode, string Output, string Error);
