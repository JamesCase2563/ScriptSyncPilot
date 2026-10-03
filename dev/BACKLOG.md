# Backlog

## Pattern Insertion Tool

**Status:** Future feature; details to be refined.

Add a pattern generator to the script editor. Users select a movement pattern, set its movement range, and specify the target section by duration or cycle count.

### Cadence

Users can count cycles while a video plays for an observation interval lasting seconds to minutes, then enter the cycle count and elapsed time. The tool calculates cycles per minute and generates the selected pattern at that cadence. A cycle means one complete bottom-to-top-to-bottom movement; keep cycles distinct from individual directional strokes.

### Pattern Profiles

Profiles shape how movement time is distributed within each cycle, squeezing or stretching the profile to fit the target cadence. Examples include:

- Fast rise followed by a slow return.
- Slowing down near each direction change.

### Open Design Decision

How generated content interacts with existing actions in the target range, including overwrite behavior, remains undecided.