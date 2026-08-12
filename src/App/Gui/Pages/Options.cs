using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Optimizer.Gui.Pages;

public sealed class MenuOption : INotifyPropertyChanged
{
    private bool? _checked = true;
    private bool _expanded;

    private MenuOption(string id, string label, List<MenuOption> children)
    {
        Id = id;
        Label = label;
        Children = children;
        foreach (var child in children) child.Parent = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; }
    public string Label { get; }
    public List<MenuOption> Children { get; }
    public MenuOption? Parent { get; private set; }

    public bool HasChildren => Children.Count > 0;

    public bool? IsChecked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            Raise();

            if (value is bool state)
            {
                foreach (var child in Children) child.Push(state);
            }
            Parent?.Rebalance();
        }
    }

    public bool IsExpanded
    {
        get => _expanded;
        set
        {
            if (_expanded == value) return;
            _expanded = value;
            Raise();
        }
    }

    internal static List<MenuOption> Build(IEnumerable<MenuEntry> entries, ISet<string> hidden)
    {
        var options = new List<MenuOption>();

        foreach (var entry in entries)
        {
            var children = Build(entry.Children, hidden);
            var option = new MenuOption(entry.Id, Loc.T(entry.LabelKey, entry.Label), children)
            {
                _checked = !hidden.Contains(entry.Id),
            };

            if (children.Count > 0) option.Rebalance();
            options.Add(option);
        }
        return options;
    }

    public static void SetAll(IEnumerable<MenuOption> options, bool state)
    {
        foreach (var option in options) option.Push(state);
    }

    public static void CollectHidden(IEnumerable<MenuOption> options, List<string> into)
    {
        foreach (var option in options)
        {
            if (option.IsChecked == false) into.Add(option.Id);
            CollectHidden(option.Children, into);
        }
    }

    private void Push(bool state)
    {
        _checked = state;
        Raise(nameof(IsChecked));
        foreach (var child in Children) child.Push(state);
    }

    private void Rebalance()
    {
        if (Children.Count == 0) return;

        bool? next = Children.All(child => child.IsChecked == true) ? true
                   : Children.All(child => child.IsChecked == false) ? false
                   : null;

        if (_checked == next) return;
        _checked = next;
        Raise(nameof(IsChecked));
        Parent?.Rebalance();
    }

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name ?? nameof(IsChecked)));
}
