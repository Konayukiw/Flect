using System.Diagnostics;
using System.Text;

namespace Flect.External;

internal sealed class ProcessFailure(string message, int exitCode) : InvalidOperationException(message)
{
    public int ExitCode { get; } = exitCode;

    public bool Crashed => ExitCode < 0;
}
