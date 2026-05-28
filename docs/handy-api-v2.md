# Handy API v2 Documentation

## Overview
Handy API v2 (2.0.0-beta-3, OAS 3.0) provides an HTTP interface for interacting with The Handy device. This API is compatible with devices running firmware version 3 (v3) only. Some endpoints are available for firmware v2 devices, but most require an update to v3.

- [OpenAPI Spec (YAML)](https://handyfeeling.com/api/handy-rest/v2/docs/spec.yaml)

## Server Selection
- **Production:** https://www.handyfeeling.com/api/handy/v2
- **Staging:** https://staging.handyfeeling.com/api/handy/v2

## Firmware v2 Devices
- Only `/connected` and `/info` endpoints work for firmware v2 devices.
- All other endpoints require firmware v3 or newer.
- Always check firmware status with `/info` (fwStatus = UPDATE_REQUIRED(2) for v2 devices).
- Update devices at [handyfeeling.com](https://www.handyfeeling.com/).

## Mode-Specific Operations
- BASE, SLIDE, TIMESYNC, and HSTP operations are mode-independent.
- Other operations (HAMP, HDSP, HSSP, etc.) require the device to be in the corresponding mode (set via `/mode`).

## Endpoints
### BASE
- `GET /mode` — Get current mode
- `PUT /mode` — Set mode
- `GET /connected` — Check connectivity
- `GET /info` — Get device info
- `GET /settings` — Get device settings
- `GET /status` — Get device status

### HAMP (Handy Alternate Motion Protocol)
- `PUT /hamp/start` — Start alternating motion
- `PUT /hamp/stop` — Stop alternating motion
- `GET /hamp/velocity` — Get velocity
- `PUT /hamp/velocity` — Set velocity
- `GET /hamp/state` — Get HAMP state

### HDSP (Handy Direct Streaming Protocol)
- `PUT /hdsp/xava` — Set absolute position/velocity
- `PUT /hdsp/xpva` — Set percent position/absolute velocity
- `PUT /hdsp/xpvp` — Set percent position/percent velocity
- `PUT /hdsp/xat` — Set absolute position/time
- `PUT /hdsp/xpt` — Set percent position/time

### HSSP (Handy Synced Stream Protocol)
- `PUT /hssp/play` — Start script
- `PUT /hssp/stop` — Stop script
- `PUT /hssp/setup` — Setup script sync
- `GET /hssp/loop` — Get loop setting (fw >= 3.2.x)
- `PUT /hssp/loop` — Set loop setting (fw >= 3.2.x)
- `GET /hssp/state` — Get HSSP state

### HSTP (Handy Simple Timing Protocol, fw >= 3.2.x)
- `GET /hstp/time` — Get device time
- `GET /hstp/offset` — Get offset
- `PUT /hstp/offset` — Set offset
- `GET /hstp/rtd` — Get round-trip delay
- `GET /hstp/sync` — Sync device with server

### MAINTENANCE (to be removed from public API)
- `PUT /maintenance/restart` — Restart device
- `PUT /maintenance/update/perform` — Perform firmware update
- `GET /maintenance/update/status` — Get update status

### OTA (to be removed from public API)
- `GET /ota/latest` — Get latest firmware

### SLIDE
- `GET /slide` — Get slide settings
- `PUT /slide` — Set slide settings
- `GET /slide/position/absolute` — Get current slide position

### TIMESYNC
- `GET /servertime` — Get current server time

## Community & Support
- Discord: Handy#8756
- Email: lars@sweettech.no
- Reddit: [r/theHandy/](https://www.reddit.com/r/theHandy/)

---
*Extracted and summarized from [Handy API v2 Docs](https://handyfeeling.com/api/handy/v2/docs/#/) on May 28, 2026.*
