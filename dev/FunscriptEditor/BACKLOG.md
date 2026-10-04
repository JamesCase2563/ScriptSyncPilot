# FunscriptEditor Backlog

## Pattern Insertion Tool

**Priority:** To be decided. **Status:** Future feature; details to be refined.

Insert a repeated movement pattern at the playhead. Users select a profile, set movement range, and specify the target section's duration or cycle count.

### Cadence

Count full cycles while a video plays over an observation interval lasting seconds to several minutes, then enter count and elapsed time. Calculate cycles per minute as `60 * cycleCount / elapsedSeconds`. A full cycle is bottom-to-top-to-bottom, distinct from one directional stroke.

### Profiles

Distribute movement time within each cycle, squeezing/stretching the profile to match measured cadence. Candidates: triangle wave, fast-up/slow-down, and slowing near direction changes.

### Conflict Handling

When a new section conflicts with existing destination values, ask the user to choose:

- Stop: cancel the insertion and leave existing values unchanged.
- Replace: replace existing values in the destination section with the new values, preserving content outside that section.
- Merge: combine the new section with existing values.

If there is no conflict, insert the section without asking. Apply the same rule to track copying and conversion. Exact collision resolution when merging values at the same timestamp remains a design detail to define.

## Undo and Redo

**Priority:** To be decided. **Status:** Proposed.

Support reversible insertion/deletion, dragging, and pattern generation. Treat a completed drag/generated segment as a coherent edit.

## Markers and Snapping

**Priority:** To be decided. **Status:** Proposed.

Evaluate markers and configurable grid/marker snapping for alignment with video, retaining precise unsnapped editing.

## Multiple Tracks and Project Format

**Priority:** To be decided. **Status:** Future feature; design to be refined.

Support multiple typed timeline tracks aligned to a shared video timebase. Tracks can contain point events, time ranges, continuous signals, or referenced media, depending on their type. Allow multiple tracks of every type, with user-defined names and independent selection, visibility, locking, ordering, and applicable playback/output settings.

### Track Types

- Funscript movement: editable timestamp/position actions, optionally associated with a device/channel.
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