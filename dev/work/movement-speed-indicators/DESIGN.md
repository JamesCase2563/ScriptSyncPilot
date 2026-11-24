# Movement Speed Indicators — Design Decisions

**Issue:** [#34 — Movement Speed Indicators](https://github.com/Frank-Yong/ScriptSyncPilot/issues/34)  
**Status:** In Progress; color-scale decisions are complete and device-limit warnings are deferred from issue #34.
**Related plan:** [PLAN.md](PLAN.md)

## Purpose and terminology

The editor's first speed value describes a segment in script space:

```text
average script speed (percentage points/second)
    = abs(end position - start position) * 1000 / elapsed milliseconds
```

The pure `SegmentSpeedCalculator` returns signed elapsed milliseconds, absolute position distance, nullable average pp/s, and a timing status (`Valid`, `DuplicateTimestamp`, or `DecreasingTimestamp`). A speed exists only when elapsed time is positive; zero or negative time keeps its measured duration and distance but has no finite speed. A positive-duration zero-distance pair has valid timing and speed zero.

It is an average over the segment at normal (1x) playback. It is not a measurement of a physical actuator and does not, by itself, say whether a movement is safe or achievable by a particular device.

## Decision 1: Color scale and palette

**Status: Decided.**

The scale is independent of timeline zoom and visible time range. Fit mode is script-specific; Manual range provides cross-script comparison.

| Considered mode | Description | Decision |
| --- | --- | --- |
| Manual range | User sets global minimum and maximum in preferences | Included for repeatable comparisons across scripts |
| Fit to script extremes | Scale to the actual minimum and maximum of every valid segment | Rejected as the default; a single marker outlier compresses ordinary values |
| Robust Fit to script | Lower bound 0; upper bound P99 rounded up to the next 100 pp/s | Selected default; fitted across the whole script and stable during zoom/scroll and point dragging |
| Fixed absolute bands | Example: 0–100, 100–200, 200–400, 400–700, over 700 pp/s | Not selected; a continuous ramp is preferred |

### Selected behavior

1. **Default mode: Fit to script.** Calculate using all valid adjacent segments in the loaded script, not only the visible viewport. Recalculate on script load and when the user explicitly invokes Fit to script. Do not recalculate continuously during point dragging.
2. **Fit bounds:** set the lower color bound to `0 pp/s`. Find P99 using the nearest-rank percentile over valid segment speeds; round the upper bound up to the next `100 pp/s`. Enforce a minimum upper bound of `100 pp/s` so an empty or zero-speed script still has a usable range. Ignore invalid-timing segments for the percentile.
3. **Manual range:** the preferences dialog lets the user set min/max values and saves them as application preferences. Use the same manual range across scripts. Require `min < max`; clamp values below/above the range to the endpoint colors.
4. **Color saturation cap:** the maximum affects color mapping only. Speeds above the maximum use the top color; retain the action and show its actual numeric speed, with a note that its color is capped.
5. **Color transfer:** use a square-root transform within the selected range so low-to-middle speeds occupy more of the palette. Preserve the minimum and maximum endpoints; show actual pp/s in the readout.
6. **Palette:** use the single vivid Jet-style rainbow from low to high. No palette selector in the first release.

### Color saturation cap

Authoring scripts may include sparse marker actions that create extremely high segment-average pp/s, even though those segments are not intended as continuous movement. The selected color-scale maximum (saturation cap) maps speeds above it to the maximum color; it does not remove, ignore, or rewrite those actions. The numeric readout continues to show the actual calculated speed and notes when its color is capped. This keeps marker outliers from compressing useful colors without hiding their values.

The cap is a display setting, not a device speed limit.

### Sample fit check — 2026-11-23

The user-provided `SLR_WankzVR_Back in the Day_2160p_2767_LR.tempo.funscript` was parsed from its `actions` array. The analysis used adjacent action pairs and calculated `abs(position delta) * 1000 / timestamp delta in ms` for positive timestamp deltas.

- 5,274 actions and 5,273 adjacent segments; no non-increasing timestamps.
- Speeds: median approximately **300.4**, 90th percentile **541.7**, 95th percentile **640**, nearest-rank 99th percentile **680**, maximum **1,882.4 percentage points/second**.
- **66.1%** of segments exceed 200 percentage points/second, so both the example 0–200 bands and the 0–200 continuous ramp would clamp or saturate most segments.
- Candidate bands 0–100 / 100–200 / 200–400 / 400–700 / over 700 contain respectively **15.9% / 18.0% / 38.7% / 27.3% / 0.2%** of this sample's segments.
- Fitting the continuous ramp to the absolute maximum of **1,882.4** would map the sample's P95 speed of **640** to about 34% of the ramp and its median of **300.4** to about 16%. That makes the absolute-extremes proposal vulnerable to outlier compression in this sample.
- The highest observed segment is 64 percentage points in 34 ms. This is a script-space average and does not establish a device limit or physical speed.

### Palette choice

**Status: Decided (updated 2026-11-24).** Use a vivid Jet-style rainbow from low to high to make adjacent speeds easier to distinguish by hue. Pair it with a labeled legend and numeric readout, and provide a high-contrast alternative. Color never stands alone as the value indication.

The implemented low-to-high ramp uses Jet-style stops: navy `#000080`, blue `#0000FF`, azure `#007FFF`, cyan `#00FFFF`, green `#7FFF7F`, yellow `#FFFF00`, orange `#FF7F00`, red `#FF0000`, and dark red `#800000`, interpolated between stops. In Windows high contrast, segment strokes use the system highlight color with system-color outlines; the legend labels this simplified system-color treatment and directs users to the numeric hover readout.

**Palette customization: Decided.** The first release uses the single selected palette; there is no user palette selector.

This keeps the low-to-high interpretation, legend, and high-contrast presentation consistent. Palette choice is fixed for the first release.

The supplied sample's nearest-rank P99 is 680 pp/s, so its fitted range rounds to `0–700 pp/s`; about 0.2% of segments exceed 700. Its maximum, 1,882.4 pp/s, remains numerically visible but uses the top color.

The palette communicates increasing script-space speed only, not device capability or safety. Pair it with a labeled legend and numeric readout, and provide a high-contrast alternative.

The implemented scale model fits from every valid adjacent segment in the loaded script, regardless of viewport or zoom. It calculates nearest-rank P99, rounds the upper bound up to the next 100 pp/s, and uses a 100 pp/s minimum upper bound. Manual bounds are validated and persisted through `EditorSettings`. A color mapping returns a normalized position plus low/high clamp status; it never changes the segment's measured speed. Fit mode recalculates on script load or explicit Fit invocation, not while editing.

**Non-linear mapping decision — 2026-11-24:** after endpoint clamping, transform the linear normalized position with `sqrt(position)`. For the example 0–700 pp/s range, 150 pp/s moves from 21% to about 46% through the palette. The legend samples this same transform and labels it `sqrt scale`; the numeric readout remains the actual segment speed.

## Decision 2: Device capability warnings

**Status: Deferred from issue #34.** The speed indicator will show script-space segment speed only. Users can choose a manual color range that helps them recognize speeds relevant to their current device setup, but the editor will not detect the device's limit, calculate physical actuator speed, or label segments as safe/unsafe. The colors are not a device warning or a measured capability indicator.

Revisit device-limit comparison only as separate work if users later need an explicit, data-backed capability warning.

## M5 proposal: share speed colors with HandySync

**Architecture implemented in M5.2; column aggregation decided in M5.1.** `ScriptSyncPilot.Common` now owns the speed calculation, scale fitting, square-root mapping, clamp status, and palette RGB stops. The palette returns a platform-neutral `RgbColor`; rendering, controls, and high-contrast treatment stay in the WinForms applications. Common has no WinForms or `System.Drawing` dependency.

Each app will store its own speed-color settings in its existing JSON settings file, with matching field names and meanings for enabled state, scale mode, and manual minimum/maximum. The apps do not share or synchronize values. Given the same speed and same range, the Common mapping and palette will return the same color; choosing different ranges in each app can intentionally produce different colors.

HandySync's `AppSettings` now mirrors the editor's speed-color fields, and its `Speed colors…` dialog edits its independent values. `ScriptWaveformControl` applies the Common range and palette to the per-column maximum speed while retaining the existing position min/max waveform. High-contrast outlines remain local to the control.

HandySync draws a whole-script min/max waveform compressed into pixel columns, while the editor paints individual adjacent-action segments. For example, if 30 seconds of script are squeezed into 300 pixels, each pixel covers 100 ms and can contain several action segments. The existing waveform combines their positions into one vertical min/max mark, so it cannot display a separate color for each overlapping segment. **Decision:** color each column using the maximum valid speed among segments overlapping that pixel's time interval, mapped with the shared color model. The waveform shape continues using the existing position min/max. This keeps the fastest movement visible in a compressed overview.

## Interaction and rendering rules

- Point hit targets win over segment hover; hovering a point must not start segment hover behavior.
- **Readout placement:** use a compact persistent row directly below the timeline and above the seek/point-navigation controls. Keep it distinct from the transport row's playback time label. Identify the values as script-space average speed at 1x.
- **Context priority:** active point drag; value-bar insertion/replacement preview under the pointer; point under the pointer; segment under the pointer; selected point when the pointer is over no other meaningful target. Clear the readout if no context applies. Pan and scrub suppress hover contexts and leave only selected-point fallback, if one exists. Hover/preview never changes selection or edits the script.
- Hovering over the value bar previews an action at the current playhead time and the position represented by the pointer's Y coordinate. Show the incoming/outgoing segment values that would result if that preview were committed. If an action already exists at the playhead timestamp, preview its replacement. Clicking commits through existing value-bar behavior.
- A point context shows separate incoming and outgoing rows; a segment context shows one row. Format duration as whole milliseconds, absolute position change as whole percentage points (`pp`), and speed to one decimal `pp/s`, with units shown. Use invariant numeric formatting. During a point drag, calculate from the in-progress action list and refresh both neighboring values on every edit. Show `No incoming segment` / `No outgoing segment` at endpoints.
- A positive-duration zero-distance segment displays `0.0 pp/s` and uses the zero-speed color. Non-increasing timestamp pairs display `Invalid timing` with the signed duration (`0 ms` or a negative millisecond count), omit speed, and receive no speed color. For values outside the color range, display the actual speed and mark `color capped` or `color capped low`; only color mapping is clamped.
- **Preferences:** segment coloring is on by default and can be disabled. Choose `Fit to script` or `Manual`; Manual exposes application-wide minimum and maximum pp/s fields shared across scripts. Require minimum less than maximum and preserve the last valid values if validation fails. `Fit to current script` recalculates using all valid segments; Fit mode recalculates on script load and explicit Fit invocation, not continuously during edits or point dragging. Manual bounds remain unchanged on script load. With no loaded script, Fit is unavailable and the most recent valid range may remain visible, but no segment data is colored. Persist the toggle, scale mode, and manual bounds in the existing `EditorSettings` settings file.
- Segment hover and color lookup use the same adjacent action pair and the same pure speed calculation.
- First color the existing connecting segments. If colored lines are not clear enough at practical zoom levels, supplement them with a narrow time-aligned color strip below the waveform using the same mapping; do not turn it into a separate speed graph.
- Speed calculation and speed-to-color mapping take no zoom, viewport, or display-coordinate inputs. Automated coverage should verify an unchanged mapped color for the same speed while zoom and view start change.
- Invalid timing has an explicit label and no finite speed/color classification. A positive-duration zero-distance segment has speed zero and receives the scale's zero-speed color.
- Color is supplementary. Keep numeric values, text legend labels, and high-contrast behavior available.
