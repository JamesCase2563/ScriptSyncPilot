using ScriptSyncPilot.Common;

namespace ScriptSyncPilot.FunscriptEditor;

public enum TimelineMovementReadoutKind
{
    Point,
    ValueBarPreview,
    Segment,
}

/// <summary>Measurements to display for the current timeline hover/selection context.</summary>
public readonly record struct TimelineMovementReadout(
    TimelineMovementReadoutKind Kind,
    FunscriptAction? PointAction,
    SegmentSpeed? Segment,
    bool HasIncomingSegment,
    SegmentSpeed? IncomingSegment,
    bool HasOutgoingSegment,
    SegmentSpeed? OutgoingSegment);
