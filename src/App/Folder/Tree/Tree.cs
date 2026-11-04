using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace Flect.Folder.Tree;

internal sealed class Tree(Request request) : TaskBase(request)
{
    private string _output = string.Empty;

    public override string Title => Loc.T("menu.folder.tree");

    public override Task RunAsync(ITaskProgress progress) => Task.Run(() =>
    {
        progress.Indeterminate();
        _output = TreeText.Render(Request.Paths, Preferences.Current.Folder.Tree, progress);
    }, progress.Token);

    public override void Present() => new Flect.Text.Result(Title, _output).Show();
}
