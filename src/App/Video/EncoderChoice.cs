using System.Diagnostics;
using System.Globalization;

namespace Flect.Video;

internal sealed record EncoderChoice(string Name, HardwareEncoder Hardware)
{
    public bool IsHardware => Hardware != HardwareEncoder.None;
}
