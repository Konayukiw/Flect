using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace Flect.Dispatch;

public sealed record LogEntry(LogSeverity Severity, string Message);
