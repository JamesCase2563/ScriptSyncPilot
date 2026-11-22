# Race condition Solutions

# Suggestion 1

I’d keep a separate pending navigation target while paused. Each next/previous click should choose from the latest requested target—not from mpv’s last observed position—and send an absolute seek to that action. Keep the requested position visible while the seek is pending; once mpv reports a position near the newest target, reconcile to its actual frame position. If the seek fails or never settles, time out and restore the observed position rather than leaving the UI stuck.

To confirm the race before changing behavior, log each navigation target, seek dispatch, and paused  time-pos  observation with timestamps. If observations are arriving out of order or showing the previous position after a seek, that’s strong evidence. I’d avoid briefly unpausing as a workaround; it can cause visible motion and alter playback state.

# Suggestion 2

I’d solve it with a paused navigation cursor: after the editor requests a seek, keep that target as the navigation position while paused, so each Next/Previous command advances from the last requested point. Tag seeks with a generation number so delayed position updates from earlier seeks can’t reset a newer target. Reconcile with mpv when it reports the current target or playback resumes.

A follow-up get_property time-pos after the seek could confirm mpv’s paused position, but I’d use that as verification rather than making every button click wait on it. I’d add tests where stale paused updates arrive after one or more rapid seeks, in both directions.
