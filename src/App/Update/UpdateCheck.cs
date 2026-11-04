using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Flect.Update;

internal sealed record UpdateCheck(UpdateStatus Status, Version? Version = null, AssetInfo? Setup = null);
