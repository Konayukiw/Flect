namespace Optimizer.Gui.Dialogs;

internal enum ScrubTarget
{
    Thumb,
    Track,
}

internal sealed class ScrubDrag
{
    public bool Active { get; private set; }

    public double? Begin(ScrubTarget target, double seconds)
    {
        if (target == ScrubTarget.Thumb)
        {
            Active = false;
            return null;
        }

        Active = true;
        return seconds;
    }

    public double? Drag(double seconds) => Active ? seconds : null;

    public double? End(double seconds)
    {
        if (!Active) return null;
        Active = false;
        return seconds;
    }

    public void Cancel() => Active = false;
}
