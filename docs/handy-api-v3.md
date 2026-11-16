# Handy API v3 (Beta) Documentation

## Overview
Handy API v3 provides an HTTP interface for interacting with Handy devices. It is a complete rewrite of the previous version, offering new protocols, authentication, and event features. This API is currently in beta (v3.0.0, OAS 3.0), but the core functionality is considered stable.

- [OpenAPI Spec (YAML)](https://www.handyfeeling.com/api/handy-rest/v3/docs/spec.yaml)
- [Full API Docs](https://links.handyfeeling.com/docs-api-handy-rest-v3)

## Key Features
- **Authentication required** for all device operations (register at [Handyfeeling User web](https://user.handyfeeling.com/)).
- **New protocols** for advanced device control (HVP, HSP, STREAM, etc.).
- **SSE (Server-Sent Events)** for real-time device event subscriptions.
- **Backward compatibility**: v2 protocols still supported, but request/response formats have changed.

## Authentication
- All device operations require authentication.
- Obtain a client API access token via `GET /auth/token/issue`.
- See [authentication guide](https://links.handyfeeling.com/docs-api-handy-rest-v3-auth).

## Protocols & Endpoints
### HAMP (Handy Alternate Motion Protocol)
- `GET /hamp/state` — Get current HAMP state
- `PUT /hamp/start` — Start HAMP protocol
- `PUT /hamp/stop` — Stop HAMP protocol
- `PUT /hamp/velocity` — Set velocity
- `PUT /hamp/stroke` — Set stroke region

### HVP (Handy Vibration Protocol)
- `GET /hvp/state` — Get current HVP state
- `PUT /hvp/state` — Set HVP state
- `PUT /hvp/start` — Start HVP playback
- `PUT /hvp/stop` — Stop HVP playback

### HSP (Handy Streaming Protocol)
- `PUT /hsp/setup` — Setup HSP session
- `PUT /hsp/add` — Add points to HSP buffer
- `PUT /hsp/play` — Start playback
- `PUT /hsp/stop` — Stop playback
- `GET /hsp/state` — Get HSP state
- ...and more (pause, resume, loop, playback rate, etc.)

### STREAM
- `PUT /stream/setup` — Setup stream protocol
- `PUT /stream/play` — Start playback
- `PUT /stream/stop` — Stop playback
- `GET /stream/state` — Get stream state

### HSSP, HDSP, HRPP, HSTP, SLIDER, SSE
- Additional protocols for script synchronization, direct streaming, repeating patterns, timing, slider control, and event subscriptions.

## Utilities & Info
- `GET /servertime` — Get current server time
- `GET /settings/slider` — Get slider settings
- `GET /info` — Get device info
- `GET /mode` / `PUT /mode` — Get/set device mode
- `GET /connected` — Check device connectivity
- `GET /capabilities` — Get device capabilities

### Connection diagnostics
HandySync queries `GET /info` once on each connection as part of its firmware check.
The window log and persistent HandySync log record the returned hardware model name
(`hw_model_name`), model number (`hw_model_no`), and model variant (`hw_model_variant`),
alongside the existing firmware version/status diagnostics. Missing hardware fields
are logged as `not reported`; a failed info request is logged and device controls
remain disabled, without changing the existing HSSP connection flow.
Only these hardware fields are logged, not the raw info response or session ID.
The API does not expose a separate manufacturer or hardware-revision field; do not
treat the model variant as a confirmed hardware revision.

## Firmware Requirements
- Devices must be on firmware v4 or newer to use API v3.
- See [Handling older firmware](https://links.handyfeeling.com/docs-api-handy-rest-v3-fw3) for migration.
- ScriptSyncPilot does not update firmware. Use the manufacturer's official guidance linked above to check and perform any required update.

## Community & Support
- Discord: Handy#8756
- Email: lars@ohdoki.com
- Reddit: [r/theHandy/](https://www.reddit.com/r/theHandy/)

---
*Extracted and summarized from [Handy API v3 Docs](https://www.handyfeeling.com/api/handy-rest/v3/docs/) on May 28, 2026.*
