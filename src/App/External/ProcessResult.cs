using System.Diagnostics;
using System.Text;

namespace Flect.External;

internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}
