# Handy API (extracted from ScriptPlayer source)

This file contains the Handy REST/API endpoints and usage patterns discovered in the repository.

Source files inspected
- `ScriptPlayer.HandyApi/HandyApiV3.cs`
- `ScriptPlayer.HandyApi/HandyController.cs`

Base configuration
- Default API base URL used in the code: `https://www.handyfeeling.com/api/handy-rest/v3/` (an API endpoint, not a web page).
- Default headers set by the client in `HandyApiV3`:
  - `X-Connection-Key: <deviceId or key>`
  - `X-Api-Key: .oBFYi~F2Ahyn~H3Q9zGMEHLGPSnCx7b` (note: this application key is specific to ScriptPlayer; register your own app if reusing.)

Endpoints discovered (relative paths)
- `mode` — GET current mode; `mode2` — PUT mode (body contains mode int).
- `connected` — GET connection status.
- `info` — GET device info (firmware status, etc.).

HSSP (handy script server protocol) endpoints
- `hssp/state` — GET state
- `hssp/setup` — PUT setup
- `hssp/play` — PUT play with payload
- `hssp/stop` — PUT stop
- `hssp/synctime` — PUT sync time

HSTP (handy script timing protocol) endpoints
- `hstp/info` — GET device time/info
- `hstp/offset` — GET/PUT offset

HAMP (handy motion/protocol) endpoints
- `hamp/velocity` — PUT velocity
- `hamp/start` — PUT start
- `hamp/stop` — PUT stop
- `hamp/state` — GET state

Slider endpoints
- `slider/state` — GET
- `slider/stroke` — GET/PUT stroke settings

Script hosting upload
- Script upload endpoint used by `HandyController`: `https://www.handyfeeling.com/api/hosting/v2/upload` (multipart form POST; form field name `file`; this endpoint does not serve a web page).

Client behavior notes
- The application reads firmware status (`info` response) for compatibility checks; it does not initiate firmware updates. See the [manufacturer's official firmware guidance](https://links.handyfeeling.com/docs-api-handy-rest-v3-fw3).
- The `HandyController` implements offset calculation and clock synchronization using endpoints under `hstp`/`hssp`.
- Script payloads are often sent as CSV via the hosting API and then referenced by URL in `hssp` calls.

Where to find official docs
- The `HandyApiV3` source references API docs and a Notion page:
  - `https://www.handyfeeling.com/api/handy-rest/v3/docs/`
  - `https://ohdoki.notion.site/Handy-API-v3-ea6c47749f854fbcabcc40c729ea6df4`
