# Code Review: PR #56

**Status:** Resolved

## Finding

- **P2 — Update the gesture documentation before merging** (`docs/keyboard-and-mouse-actions.md:70`): The PR implements Shift+left-drag panning, but the new reference still calls it proposed and says it is not implemented. `DESIGN.md` also says the implementation is pending, while the plan and verification docs mark it complete. Update these stale descriptions so users are not told the shipped gesture is unavailable.
