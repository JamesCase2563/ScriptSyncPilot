# Race condition Solutions

## Decision

Choose **Suggestion 2: a paused navigation cursor with generation tracking**. While mpv is paused, the latest requested point remains the timeline's navigation position, so repeated Previous/Next commands advance from the user's most recent request rather than mpv's potentially stale observation. Each seek gets a monotonically increasing generation; verification results from older generations must not replace the latest target. Since ordinary `time-pos` notifications carry no generation, ignore them while a paused seek is pending. Reconcile through a correlated position query for the current generation or when playback resumes.

After dispatching a paused seek, start a delayed `get_property time-pos` query without making the button click wait. If the first query still sees the old position, retry the same absolute seek once and continue polling. If the latest seek is confirmed, reconcile to the queried position. If repeated queries still report another position or time out, report that but keep the requested cursor and continue low-rate polling until it is confirmed, superseded, or playback resumes. Do not restore the cursor from an unconfirmed paused observation. A failed seek write can immediately restore the last observed position. Keep playback behavior unchanged and do not briefly unpause.

This combines Suggestion 2's handling of multiple/out-of-order seeks with Suggestion 1's explicit failure handling. It avoids making a user wait for a property query on every click.

# Suggestion 1

I’d keep a separate pending navigation target while paused. Each next/previous click should choose from the latest requested target—not from mpv’s last observed position—and send an absolute seek to that action. Keep the requested position visible while the seek is pending; once mpv reports a position near the newest target, reconcile to its actual frame position. If the seek fails or never settles, time out and restore the observed position rather than leaving the UI stuck.

To confirm the race before changing behavior, log each navigation target, seek dispatch, and paused  time-pos  observation with timestamps. If observations are arriving out of order or showing the previous position after a seek, that’s strong evidence. I’d avoid briefly unpausing as a workaround; it can cause visible motion and alter playback state.

# Suggestion 2

I’d solve it with a paused navigation cursor: after the editor requests a seek, keep that target as the navigation position while paused, so each Next/Previous command advances from the last requested point. Tag seeks with a generation number so delayed position updates from earlier seeks can’t reset a newer target. Reconcile with mpv when it reports the current target or playback resumes.

A follow-up get_property time-pos after the seek could confirm mpv’s paused position, but I’d use that as verification rather than making every button click wait on it. I’d add tests where stale paused updates arrive after one or more rapid seeks, in both directions.
