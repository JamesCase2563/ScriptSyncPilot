# FunscriptEditor Backlog

## Table of Contents

- [Pattern Insertion Tool](#pattern-insertion-tool)
- [Undo and Redo](#undo-and-redo)
- [Markers and Snapping](#markers-and-snapping)
- [Video Playback Speed](#video-playback-speed)
- [Keyboard Numeric Position Entry](#keyboard-numeric-position-entry)
- [Tap-Based Pattern Generation](#tap-based-pattern-generation)
- [Editable Pattern Preview](#editable-pattern-preview)
- [Expressive Patterns and Controlled Variation](#expressive-patterns-and-controlled-variation)
- [Movement Speed Indicators](#movement-speed-indicators)
- [Author-Guided Video Motion Detection](#author-guided-video-motion-detection)
- [Live Pattern Recording](#live-pattern-recording)
- [Multiple Tracks and Project Format](#multiple-tracks-and-project-format)
	- [Track Types](#track-types)
	- [Multiple Tracks of the Same Type](#multiple-tracks-of-the-same-type)
	- [Copying and Conversion](#copying-and-conversion)
	- [Video Chapters](#video-chapters)
	- [Project Persistence](#project-persistence)
- [Verification and Boundaries](#verification-and-boundaries)

## Pattern Insertion Tool

**Priority:** Selected next. **Status:** Implemented; generator, conflict planner, editor application, and multilevel undo/redo integrated. Automated and manual verification results are recorded; quantitative large-script memory/performance measurement is deferred.

Archived documents: [plan](../archive/2026-10-pattern-insertion/PLAN.md), [design](../archive/2026-10-pattern-insertion/DESIGN.md), and [verification](../archive/2026-10-pattern-insertion/VERIFICATION.md).

Insert a repeated movement pattern at the playhead. Users select a profile, set movement range, specify the target section's start/end times, and enter the full-cycle count directly. Show the output cadence implied by those inputs.

### Cadence

The output interval and selected full-cycle count determine cycles per minute as `60 * cycleCount / outputDurationSeconds`. Do not require a separate observation interval or infer/round the count from measured cadence. A full cycle is bottom-to-top-to-bottom, distinct from one directional stroke.

### Profiles

Distribute movement time within each cycle to fit the selected count exactly into the output interval. Profiles: triangle wave, configurable asymmetric cycle, and slowing near direction changes.

### Conflict Handling

When a new section conflicts with existing destination values, ask the user to choose:

- Stop: cancel the insertion and leave existing values unchanged.
- Replace: replace existing values in the destination section with the new values, preserving content outside that section.
- Merge: combine the new section with existing values.

If there is no conflict, insert the section without asking. Actual interior value-points count as conflicts, not interpolation between outside points. Preserve existing boundary points instead of competing generated endpoints, and preview the resulting transitions. Merge remembers the user's Keep Existing / Use New choice for identical interior timestamps. Apply the same conflict rule to track copying and conversion.

Fit the directly selected whole-cycle count to the output interval; never emit partial endings. Offer Continue Existing / Pattern Start and profile settings suitable for a future pattern editor. Keep all pattern dialog values across application sessions. Initial defaults are 25% rise / 75% return, cosine easing with one-position-unit adaptive sampling, a configurable three-hour section limit, and a 100,000-action generation cap. These are editor safeguards, not verified Handy API limits. See the working design for details.

## Undo and Redo

**Priority:** Selected with pattern insertion. **Status:** Implemented; `TimelineControl` records delta-based edits through `ActionHistory`, with multilevel undo/redo and saved-state checkpoints. Automated and manual verification results are recorded; quantitative large-script memory/performance measurement is deferred.

Support multilevel undo/redo for insertion/deletion, dragging, and pattern generation. Treat a completed drag/generated segment as one coherent history entry. New committed edits after undo clear redo; preview/cancellation do not. This work is included in the [archived pattern-insertion plan](../archive/2026-10-pattern-insertion/PLAN.md), not a temporary one-operation rollback.

Use deltas for small edits and affected-section history for bulk edits, not whole-document snapshots for every operation. No restrictive edit-count limit is planned initially; measure memory during trial.

## Markers and Snapping

**Priority:** To be decided. **Status:** Proposed.

Evaluate markers and configurable grid/marker snapping for alignment with video, retaining precise unsnapped editing.

## Video Playback Speed

**Priority:** Implemented. **Status:** Implemented and verified (2026-10-11).

Allow changing mpv playback speed from the editor for review and timing work. Show the active rate and provide a clear way to restore normal speed (1x). Define the supported range and increments, whether the rate resets or persists when changing videos, and how speed changes interact with seeking, frame stepping, and the timeline's media-time coordinates.

Archived documents: [plan](../archive/2026-10-video-playback-speed/PLAN.md), [design](../archive/2026-10-video-playback-speed/DESIGN.md), and [verification](../archive/2026-10-video-playback-speed/VERIFICATION.md).

## Keyboard Numeric Position Entry

**Priority:** Selected. **Status:** Implemented and verified; automated tests and user-confirmed manual checks passed. Working documents archived; PR review status is not recorded.

With the timeline focused, physical top-row `1` through `9` insert 10-90% at the current playhead; `0` inserts 100%, and the key left of `1` inserts 0%. Use fixed scan codes on close-to-standard PC keyboards with Latin-like layouts. Shift/Ctrl/Alt combinations and numpad keys do not insert points; editable controls retain native behavior. Ignore held position-key repeats while retaining arrow navigation hold-repeat. Existing timestamps are updated without duplicates, edits are undoable, and identical values preserve dirty state and redo.

Archived documents: [plan](../archive/2026-10-numeric-position-entry/PLAN.md), [design](../archive/2026-10-numeric-position-entry/DESIGN.md), and [verification](../archive/2026-10-numeric-position-entry/VERIFICATION.md).

This provides a separate manual rhythm-entry workflow: press position keys at the desired video times to author individual points, rather than adding a tap-tempo control to live pattern recording. Coordinate digit bindings with the recording mode below so one keypress cannot both insert a point and change a pattern boundary.

## Tap-Based Pattern Generation

**Priority:** To be decided; suggested before editable previews and controlled variation. **Status:** Proposed.

Generate patterns from author-recorded taps, including taps entered during reduced-speed playback. Preserve their video-time timestamps by default instead of replacing them with evenly spaced cycles. This extends the manual tapping [workflow](../../docs/scripting-workflow.md) without changing the existing interval-and-cycle-count insertion mode.

- Support an explicit choice between one tap per full cycle at the same phase (generate the intermediate turning point using the chosen profile) and one tap per turning point (alternating bottom/top, preserving unequal outward and return durations).
- Offer consistent-range mode: keep the selected minimum/maximum positions, so longer intervals produce slower movement.
- Explore consistent-average-speed mode: longer intervals produce greater movement distance. For a full out-and-back cycle, average speed is approximately `2 * strokeRange / cycleDuration`; require a reference speed or a calibrated cycle because timing alone cannot determine absolute range. Define range anchoring and report infeasible ranges rather than silently clamping or distorting them.
- Make smoothing or timing regularization explicit and optional. Define tap selection, phase, incomplete-cycle handling, and invalid-input feedback before implementation; distinguish taps from ordinary script points and structure markers.
- Preview the output, preserve content outside the selected section, and reuse existing conflict handling and coherent undo. Verify exact preservation of tap timestamps at different playback rates and both interpretations of interval length.

## Editable Pattern Preview

**Priority:** To be decided; suggested after tap-based generation. **Status:** Proposed.

Treat generated output as a temporary editable draft before insertion. Let the author drag meaningful turning points in time and position and add/remove points without having to manipulate every sample used to approximate an eased curve.

- Apply the edited draft as one coherent undoable operation; cancellation leaves the script, dirty state, and committed undo/redo history unchanged.
- Apply exactly the reviewed draft rather than unconditionally regenerating and losing manual adjustments. Recheck conflicts against the current destination script and preserve existing boundary/conflict rules.
- Define how generator-setting changes interact with manual draft edits; do not silently discard edits. Keep draft operations separate from committed-script history.
- Verify that preview edits survive application, cancellation has no committed effects, and destination edits made after preview are handled explicitly.

## Expressive Patterns and Controlled Variation

**Priority:** To be decided; suggested after evaluating tap-based generation and editable previews. **Status:** Proposed; randomization is optional, not a default.

Reduce mechanical repetition through intentional variation before adding randomness: preserve observed tap timing, allow gradual changes in depth and rise/return balance, and support editable turning points and suitable easing. Compare the generated curve with actual device playback to distinguish generator limitations from device behavior.

- Evaluate small, smoothly changing variation across cycles rather than independent jitter on every sample. Timing and position variation must be opt-in and bounded.
- Keep author-tapped timing anchors fixed by default and preserve section boundaries, strictly ordered timestamps, and the author's position range.
- Prevent accidental extra reversals and excessively short movements; define measurable interval and movement-rate limits rather than assuming editor limits guarantee device safety.
- Use repeatable seeded generation and apply the exact reviewed output so preview and insertion cannot differ through rerandomization.
- Verify bounds, timestamp ordering, anchor preservation, repeatability, and transitions to surrounding content. Evaluate usefulness on representative sections before committing to randomization.

## Movement Speed Indicators

**Priority:** To be decided. **Status:** Proposed.

Make movement speed clear during detailed point editing regardless of timeline zoom; the apparent angle of a segment changes with zoom and is not a reliable speed indicator.

- Start with a numerical readout when hovering over a segment or dragging a point: duration in milliseconds, absolute position distance in percentage points, and average speed in percentage points per second, calculated as `abs(endPosition - startPosition) / elapsedSeconds`. For example, 50 percentage points over 250 ms is 200 percentage points/second.
- For a selected or dragged point, show both incoming and outgoing segment speeds and update them during the edit.
- Add optional segment coloring using a fixed speed scale independent of zoom and the visible section, with a legend and numerical readout so color is not the only indication. Do not label speeds as safe/unsafe based on script values alone.
- Describe these as script-space segment-average speeds at normal (1x) playback, not measured physical device speeds. Playback rate, configured stroke range, interpolation, and device behavior can affect actual movement.
- Consider a derived speed-graph track later, as part of [Multiple Tracks and Project Format](#multiple-tracks-and-project-format).
- Verify numerical values, incoming/outgoing updates, zero-distance segments, and zoom-independent readouts/color mapping. Do not calculate a finite speed for duplicate or non-increasing timestamps; surface invalid timing explicitly.

## Author-Guided Video Motion Detection

**Priority:** To be decided. **Status:** Idea for exploration; not selected for implementation.

Explore reducing manual tempo tapping by letting the author identify the actionable movement in a movie. Start with a short, author-selected continuous shot: select a tracking region, indicate the movement axis, and optionally select a reference feature to distinguish relevant movement from camera or whole-subject motion.

- First milestone to evaluate: propose full-cycle boundary taps for review and use with the existing [scripting workflow](../../docs/scripting-workflow.md) and pattern insertion tool.
- Possible later extension: calibrate the two movement extremes to script minimum/maximum and propose position curves, including changing stroke lengths. Screen displacement does not necessarily represent the desired script depth.
- Keep results as editable suggestions with preview, explicit acceptance, existing conflict handling, and undo. Do not automatically overwrite authored work or send unreviewed output to a device.
- Flag tracking loss and uncertain intervals for correction/reinitialization rather than inventing movement. Evaluate cuts, occlusion, blur, perspective changes, and features leaving the frame.
- Investigate timestamp-preserving frame decoding and conventional feature tracking, optical flow, or template matching; FFmpeg and an OpenCV .NET wrapper are candidates, not selected dependencies. The current mpv IPC integration supplies playback state/control, not image frames.
- For VR, evaluate a stable projection, fixed viewport, or single-eye view so head movement does not become scripted movement.
- Before committing to implementation, test representative clips and define measurable timing accuracy, tracking reliability, correction effort, and processing-performance targets. Decide whether assisted tempo detection is useful enough before extending to calibrated positions.

## Live Pattern Recording

**Priority:** To be decided. **Status:** Proposed; keyboard control groups agreed, exact bindings and cycle-time presets to be refined.

While video plays, generate movement actions continuously at video timestamps using the current minimum/maximum position and full-cycle time. User keypresses change the movement range or cadence for subsequent output, allowing a script to be authored while watching instead of inserting separately timed sections. Cycle time controls script movement frequency, not mpv playback speed.

### Range Keys

Reuse the numeric-entry physical position map: left-of-`1`=0%, `1` through `9`=10-90%, and `0`=100%. Unmodified position keys set maximum; Shift-only position keys set minimum. Ctrl/Alt/AltGr combinations do neither. Keep the same physical mapping across modes, and maintain a valid minimum/maximum range without silently changing the other boundary. This recording behavior remains proposed, not implemented by numeric point entry.

### Cycle-Time Keys

Use `Q`, `W`, `E`, `R`, `T`, `Y`, `U`, `I`, `O`, `P` as direct cycle-time presets, ordered slowest to fastest from left to right on a QWERTY keyboard. Each key selects its preset immediately, rather than incrementing/decrementing speed. Define the ten full-cycle durations and display the active cycle time/cadence. Exact values and keyboard-layout handling remain design decisions. Manual rhythm entry belongs to Keyboard Numeric Position Entry; a separate tap-tempo feature is not required here.

### Recording Contracts to Define

Use an explicit recording mode so digit keys adjust pattern boundaries only while that mode is active; outside it, Keyboard Numeric Position Entry retains its individual-point behavior. Preserve native keyboard behavior while text/numeric inputs have focus, and do not intercept modified file shortcuts such as Ctrl+O.

Define phase continuity on cycle-time changes, transitions when range changes, and start/stop/pause/seek behavior before implementation. Proposed safeguards: preserve phase when changing speed, avoid abrupt position jumps, use media time even at altered playback speed, and stop recording on seeks or document switches. Preview/review a recording take before applying the established destination conflict rules, preserve metadata and outside actions, and make each accepted take one undoable edit. Reuse Common's profile generation where suitable; do not change the existing whole-cycle section insertion contract implicitly.

## Multiple Tracks and Project Format

**Priority:** To be decided. **Status:** Future feature; design to be refined.

Support multiple typed timeline tracks aligned to a shared video timebase. Tracks can contain point events, time ranges, continuous signals, or referenced media, depending on their type. Allow multiple tracks of every type, with user-defined names and independent selection, visibility, locking, ordering, and applicable playback/output settings.

### Track Types

- Funscript movement: editable timestamp/position actions, optionally associated with a device/channel.
- Movement speed graph: a derived, read-only view of a chosen movement track's segment-average speed in percentage points/second at 1x playback, aligned to the same timebase and updated when source actions change. Use explicit source-track association and zoom-independent speed units/scales; see [Movement Speed Indicators](#movement-speed-indicators).
- Chapters: named video section boundaries, with previous/next chapter navigation.
- Project information and notes: time-linked annotations, section descriptions, scripting decisions, and work-in-progress notes; project-wide information belongs in project metadata.
- Extracted images: thumbnails or reference frames at timestamps, with links to the extracted assets.
- Audio: referenced audio clips and waveforms aligned to video, with per-track offsets and mute controls where playback is supported.
- Beat/rhythm markers: detected or manually marked beats to guide movement timing.
- Analysis signals: audio amplitude, motion intensity, or other derived measurements, kept separate from authored actions.
- Subtitles/transcripts: timed text for navigating and annotating sections.
- Review/work status: ranges marked for scripting, revision, or completion.

The additional types are candidates, not a requirement to implement them all in the first multitrack version.

### Multiple Tracks of the Same Type

Support separately named movement tracks such as male/female, Normal/Extreme, alternate takes, and different device/channel outputs. These labels are user-defined variants, not fixed categories or automatic transformations. Multiple chapter, note, image, audio, and analysis tracks should also be possible.

Users must explicitly choose which movement track to export or use for output. Display-only and analysis tracks must not implicitly drive a device; output binding and simultaneous-track conflict rules need a separate design decision.

### Copying and Conversion

Allow copying a selected time range or a whole track to another track, including duplication into a new track of the same type. Preserve source data and timing by default, with an explicit destination time/offset and undo support.

Copying between unlike types requires a defined conversion rather than treating their data as interchangeable. In particular, support an audio-to-funscript workflow that derives candidate movement actions from an audio envelope, beats, or other selected analysis. Let the user configure the mapping, timing, and position range, preview the result, and review/edit it before applying it to the destination. A waveform alone does not define the intended movement.

Other candidate conversions include beat markers to repeated movement patterns, analysis signals to movement actions, and chapter boundaries to editor markers. Reject unsupported conversions clearly. Apply the conflict-handling rule above: ask Stop, Replace, or Merge only when destination values conflict; otherwise apply the new section without a prompt.

### Video Chapters

Read existing embedded chapters through mpv and show them as chapter tracks. Allow creating, naming, editing, and navigating chapter boundaries. Save chapter edits in the project independently of the video, with an optional export/embed operation to a new video copy, leaving the original untouched.

Evaluate FFmpeg or MKVToolNix for chapter export without re-encoding where the container supports it. MKV and MP4 chapter/player compatibility must be verified; adding editor markers does not automatically create embedded video chapters. Chapters are distinct from funscript movement actions.

### Project Persistence

Define a versioned project schema covering track identity/type/name, ordered content, metadata, media references, offsets, output bindings, and relevant track settings. Keep referenced video/audio/image assets separate from timeline data and define relative-path handling so projects can be relocated.

Preserve standard funscript import/export for individual movement tracks. The richer project format must retain all track types and annotations without trying to encode them as standard funscript actions. Chapter/media conversion dependencies and export formats should be explicit.

## Verification and Boundaries

See the [shared backlog](../BACKLOG.md) for interaction tests, persistence round-trips, CI, and service separation. The former controller proposal does not imply a device controller in the editor.