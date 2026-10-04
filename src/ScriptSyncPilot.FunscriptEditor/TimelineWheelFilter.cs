namespace ScriptSyncPilot.FunscriptEditor;

/// <summary>
/// Sends wheel input to the timeline whenever the cursor is over it.
///
/// WinForms delivers WM_MOUSEWHEEL to the focused control, so without this the
/// zoom slider (or whatever was last clicked) swallows the wheel and the
/// timeline zooms about its own anchor instead of the cursor.
/// </summary>
internal sealed class TimelineWheelFilter(TimelineControl timeline) : IMessageFilter
{
    private const int WmMouseWheel = 0x020A;

    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg != WmMouseWheel) return false;
        if (timeline.IsDisposed || !timeline.Visible || !timeline.IsHandleCreated) return false;

        var lParam = (int)(long)m.LParam;
        var screenPoint = new Point((short)(lParam & 0xFFFF), (short)((lParam >> 16) & 0xFFFF));
        var client = timeline.PointToClient(screenPoint);
        if (!timeline.ClientRectangle.Contains(client)) return false;

        var delta = (short)(((long)m.WParam >> 16) & 0xFFFF);
        timeline.ZoomByWheel(delta, client.X);
        return true;
    }
}
