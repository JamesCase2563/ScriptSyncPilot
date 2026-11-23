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
5. **Palette:** use the single vivid, perceptually ordered multi-hue palette from low to high. No palette selector in the first release.

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

**Status: Decided.** Use a vivid, perceptually ordered multi-hue palette, not a classic full-spectrum rainbow. Keep its lightness progression ordered from low to high, pair it with a labeled legend and numeric readout, and provide a high-contrast alternative. Color never stands alone as the value indication.

**Palette customization: Decided.** The first release uses the single selected palette; there is no user palette selector.

This keeps the low-to-high interpretation, legend, and high-contrast presentation consistent. Revisit a small set of tested, perceptually ordered palettes only if users later need that choice.

The supplied sample's nearest-rank P99 is 680 pp/s, so its fitted range rounds to `0–700 pp/s`; about 0.2% of segments exceed 700. Its maximum, 1,882.4 pp/s, remains numerically visible but uses the top color.

The palette communicates increasing script-space speed only, not device capability or safety. Pair it with a labeled legend and numeric readout, and provide a high-contrast alternative.

## Decision 2: Device capability warnings

**Status: Deferred from issue #34.** The speed indicator will show script-space segment speed only. Users can choose a manual color range that helps them recognize speeds relevant to their current device setup, but the editor will not detect the device's limit, calculate physical actuator speed, or label segments as safe/unsafe. The colors are not a device warning or a measured capability indicator.

Revisit device-limit comparison only as separate work if users later need an explicit, data-backed capability warning.

## Interaction and rendering rules

- Point hit targets win over segment hover; hovering a point must not start segment hover behavior.
- A selected or dragged point's incoming/outgoing readout takes precedence over a hovered-segment readout.
- During a drag, calculate from the in-progress action list, not the pre-drag snapshot. Update both neighboring segment values as the point moves.
- Segment hover and color lookup use the same adjacent action pair and the same pure speed calculation.
- Speed calculation and speed-to-color mapping take no zoom, viewport, or display-coordinate inputs. Automated coverage should verify an unchanged mapped color for the same speed while zoom and view start change.
- Invalid timing has an explicit label and no finite speed/color classification. A positive-duration zero-distance segment has speed zero and receives the scale's zero-speed color.
- Color is supplementary. Keep numeric values, text legend labels, and high-contrast behavior available.
