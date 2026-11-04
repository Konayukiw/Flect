namespace Flect.Video.Compress;

internal sealed record EncodingProfile(int Width, int Height, double Fps, int VideoKbps,
                                       int AudioKbps);
