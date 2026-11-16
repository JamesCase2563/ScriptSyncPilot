# Scripting Workflow (FunscriptEditor)

A top-down process for scripting a video: mark structure quickly, then mark tempo, then let the pattern tool generate the strokes. Each pass is coarse-to-fine, so most of the video is handled at high playback speed and only the movement sections need careful attention.

## Levels

Film structure (scene, take, action, session) only matters for a script where it changes *whether* the device moves or *how fast*. The process therefore uses three levels:

| Level | Marks | Replaces | Needed? |
| --- | --- | --- | --- |
| 1. Cut | Camera cuts, pauses, scene changes | Scene, Take | Optional; useful navigation landmarks in long or busy videos |
| 2. Movement section | Where device movement starts and stops | Action, Session, Movement | Always |
| 3. Tempo | One tap per full movement cycle | Tempo | Always, inside movement sections only |

Actions and sessions (eating, then drinking) need no own level: if the movement changes, it becomes a new movement section or a tempo change; if it does not, the script does not need to know.

## Marker Values

Markers are ordinary script points, so each level uses its own position value. They stay visually separable on the timeline and never collide with tempo taps. Enter them with numeric position keys while the timeline has focus (the key left of `1` = 0%, `1`-`9` = 10-90%, `0` = 100%).

| Marker | Key | Position |
| --- | --- | --- |
| Scene change (only if the video has several scenes) | `0` | 100% |
| Cut | `5` | 50% |
| Movement start | `7` | 70% |
| Movement stop | `3` | 30% |
| Tempo tap (stroke bottom) | key left of `1` | 0% |

Mnemonic: scenes are at the ceiling, start is high, stop is low, cuts sit in the middle, taps are on the floor.

## Passes

Save after each pass. Before pass 4, also save a copy (for example `<movie>.markers.funscript`); pattern insertion replaces the taps, and the copy keeps the hand-marked structure for later rework.

### 1. Cuts (optional, 2x)

State: Scenes
Filename: `<movie>.scenes.funscript`

Play at 2x and press `5` at each cut or pause; press `0` at a change of scene. Skip this pass for short videos or a single continuous take.

| Key | When |
| --- | ---- |
|  5  | at each cut or pause |
|  0  | at a change of scene |

### 2. Movement sections (1.5-2x)

State: Movement
Filename: `<movie>.movement.funscript`

Play at 1.5-2x. Press `7` where device movement should begin and `3` where it should end. Everything between a `3` and the next `7` is a rest section and needs no further work. Use frame step (`Left`/`Right`) to refine a boundary, or drag the point.

| Key | When |
| --- | ---- |
|  7  | where device movement should begin |
|  3  | where it should end |

### 3. Tempo taps (0.5-1x, movement sections only)

Filename: `<movie>.tempo.funscript`

Seek to each `7` marker and play at 1x, or 0.5-0.75x for fast movement. Press the key left of `1` once per full cycle, always at the same phase (the bottom of the stroke). Timestamps are recorded in video time, so slowed playback gives more precise taps without distorting tempo.

The tap spacing now shows the tempo directly: even spacing means constant speed, and a visible change in spacing marks a tempo change. Correct individual taps by dragging or by deleting and retapping. Held keys do not repeat.

| Key         | When |
| ----------- | ---- |
| left of `1` | once per full cycle |

### 4. Patterns (one per constant-tempo segment)

Filename: `<movie>.draft.funscript`

For each run of evenly spaced taps:

1. Seek to the first tap of the run and copy the playhead into the dialog's start time; do the same with the last tap for the end time.
2. Set the cycle count to **taps in the run minus one** (the taps mark cycle boundaries).
3. Set min/max for the stroke, choose the profile, and check the preview.
4. Choose **Replace**. Interior taps are removed; the first and last taps stay as the segment's bottom points.

At a tempo change, the last tap of one run is the first tap of the next, so consecutive segments join without a gap. If the stroke minimum is not 0%, the preserved boundary taps sit slightly below the pattern; drag them up to the minimum or accept the small dip.

### 5. Cleanup and review (1x, with device)

Filename: `<movie>.final.funscript`

Remaining structure markers would move the device, so convert them:

- Drag each movement start (`70%`) and stop (`30%`) marker to the rest position (normally the stroke minimum). Rest sections then hold still instead of drifting.
- Delete cut and scene markers, or drag them to the rest position if they fall inside a rest section.

Finally play through at 1x with the device connected and adjust transitions at section boundaries.

## Tips

- Most of the time is spent in pass 3. Keep passes 1-2 fast and rough; boundaries can be refined later.
- Undo (`Ctrl+Z`) covers taps, drags, and whole pattern insertions.
- A section with irregular movement can keep hand-tapped points instead of a pattern: tap each turning point with the key matching its depth, so both timing and stroke length follow the video.
