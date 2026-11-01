using Optimizer.Gui.Dialogs;

namespace Evals;

public sealed class ScrubDragTests
{
    [Fact]
    public void TrackPressFollowsThePointerUntilRelease()
    {
        var drag = new Trim.ScrubDrag();

        Assert.Equal(10.5, drag.Begin(Trim.ScrubTarget.Track, 10.5));
        Assert.True(drag.Active);
        Assert.Equal(14.0, drag.Drag(14.0));
        Assert.Equal(18.75, drag.End(18.75));
        Assert.False(drag.Active);
        Assert.Null(drag.Drag(20));
    }

    [Fact]
    public void ReleaseWithoutAMoveStillUsesTheReleasePoint()
    {
        var drag = new Trim.ScrubDrag();

        Assert.Equal(10.5, drag.Begin(Trim.ScrubTarget.Track, 10.5));
        Assert.Equal(16.25, drag.End(16.25));
        Assert.Null(drag.End(16.25));
    }

    [Fact]
    public void PressOnThePointerLeavesTheThumbDragAlone()
    {
        var drag = new Trim.ScrubDrag();

        Assert.Null(drag.Begin(Trim.ScrubTarget.Thumb, 5));
        Assert.False(drag.Active);
        Assert.Null(drag.Drag(10.5));
        Assert.Null(drag.End(12));

        Assert.Equal(10.5, drag.Begin(Trim.ScrubTarget.Track, 10.5));
        Assert.Equal(11.0, drag.Drag(11.0));
    }
}
