using System.Globalization;
using System.Windows.Controls;

namespace Flect.Settings;

internal interface ISettings
{
    void Load(Settings settings);

    void Store(Settings settings);
}
