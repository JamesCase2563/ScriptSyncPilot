# Movement Speed Indicators — Design Decisions

**Issue:** [#34 — Movement Speed Indicators](https://github.com/Frank-Yong/ScriptSyncPilot/issues/34)  
**Status:** Draft; color thresholds and device-limit behavior remain undecided.  
**Related plan:** [PLAN.md](PLAN.md)

## Purpose and terminology

The editor's first speed value describes a segment in script space:

```text
average script speed (percentage points/second)
    = abs(end position - start position) * 1000 / elapsed milliseconds
```

It is an average over the segment at normal (1x) playback. It is not a measurement of a physical actuator and does not, by itself, say whether a movement is safe or achievable by a particular device.

## Decision 1: Fixed speed-color range

**Status: Open. No range has been selected.**

The issue requires the scale to be fixed across scripts, zoom levels, and visible time ranges. It does not supply thresholds or a reference dataset. The scale must therefore not be based on the current script's maximum, percentiles, zoom, or viewport.

| Option | Example | Advantages | Tradeoffs |
| --- | --- | --- | --- |
| Fixed absolute bands | 0–50, 50–100, 100–200, and over 200 percentage points/second | Easy to explain and compare between scripts; simple legend and tests | The example thresholds are provisional and need validation against real scripts; discrete bands can hide small differences |
| Fixed continuous range | Continuous color ramp from 0 to 200 percentage points/second, with values above 200 clamped to the endpoint | Shows gradual changes and remains comparable | Harder to read precisely from color; must still show numeric values and legend endpoints |
| User-configurable fixed range | User chooses lower/upper values and colors | Can fit different workflows | Users lose comparability unless settings are shared; adds settings and persistence decisions |

**Recommendation for discussion:** use a fixed absolute scale in percentage points per second. Choose either labeled bands or a continuous ramp after reviewing representative scripts. Clamp values below/above the range to the endpoint colors and keep the exact numeric readout available. The example bands above are not a decision.

Color direction should communicate only increasing script-space speed (for example, neutral/cool through warm). Do not use a red endpoint as a device or safety alert. The legend needs text labels and system-aware colors or a non-color cue for high-contrast mode.

## Decision 2: Device capability or alert coloring

**Status: Open; recommended to defer from issue #34.**

The issue explicitly limits the first readout to script-space averages and says not to label speeds safe/unsafe from script values alone. A device-limit warning needs device-specific inputs that the FunscriptEditor does not currently own.

The repository's HandySync client reads slider settings including physical limits and reported minimum/maximum speed in [HandyApiClient.cs](../../../src/ScriptSyncPilot.HandySync/HandyApiClient.cs). FunscriptEditor has no Handy device session or configured physical stroke range, so it cannot currently associate a script segment with those values. The API fields' meaning as an enforceable operating limit would also need validation before using them for warnings.

If a future feature supplies a verified physical travel range `L` in millimeters, segment speed can be estimated at 1x as:

```text
average physical speed (mm/s)
    = script speed (percentage points/second) * L / 100
```

At playback multiplier `r`, the estimate would additionally scale by `r`. This remains a segment-average estimate; device acceleration, firmware behavior, stroke configuration, and interpolation can change actual motion. If a verified maximum speed `Vmax` is available, a corresponding script-space threshold would be:

```text
maximum script speed (percentage points/second)
    = 100 * Vmax / (L * r)
```

This comparison is undefined if any input is missing, stale, invalid, or has unverified semantics. It should not silently fall back to a guessed device model or a generic red speed band.

| Option | Advantages | Tradeoffs |
| --- | --- | --- |
| No device alert in #34; show script-space speed only | Matches the issue's current scope; no false device-capability claim | Cannot flag a movement that may exceed a connected device's reported limit |
| User-configured capability overlay | Can work without a live device and can account for travel range/playback rate | Requires users to enter and maintain accurate physical settings; warning still depends on interpolation/device behavior |
| Live Handy capability comparison | Can use the connected device's reported physical range and speed settings | Requires cross-app/device integration, validation of API field semantics, freshness handling, and playback-rate integration |

**Recommendation for discussion:** keep issue #34's speed colors purely script-space and do not assign them alert meaning. If device-limit feedback is wanted, track it separately and design a distinct warning indicator that shows the inputs and threshold it compared against. Do not call it a safety rating.

## Interaction and rendering rules

- Point hit targets win over segment hover; hovering a point must not start segment hover behavior.
- A selected or dragged point's incoming/outgoing readout takes precedence over a hovered-segment readout.
- During a drag, calculate from the in-progress action list, not the pre-drag snapshot. Update both neighboring segment values as the point moves.
- Segment hover and color lookup use the same adjacent action pair and the same pure speed calculation.
- Speed calculation and speed-to-color mapping take no zoom, viewport, or display-coordinate inputs. Automated coverage should verify an unchanged mapped color for the same speed while zoom and view start change.
- Invalid timing has an explicit label and no finite speed/color classification. A positive-duration zero-distance segment has speed zero and receives the scale's zero-speed color.
- Color is supplementary. Keep numeric values, text legend labels, and high-contrast behavior available.

## Decisions needed from product owner

- Which absolute range or bands should the first release use? The example bands above are only a starting point.
- Should the scale use discrete labeled bands or a continuous ramp?
- Should users be able to change the scale, or should it remain identical for everyone?
- Confirm that device-limit warnings are deferred from #34. If not, define the supported device source, validated meaning of its speed limit, physical travel range, playback-rate handling, and warning wording before implementation.
