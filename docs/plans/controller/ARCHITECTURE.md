# ControllerHost — Architecture Notes

Core responsibilities
- Manage device adapters (Buttplug, BLE, TCP) and expose a single `Controller` output.
- Provide a command queue with latency compensation and per-device RTT tracking.
- Serve a local control API (in-process for GUI, WebSocket/REST for remote control).

Suggested components
- Adapter Manager — loads and manages adapter instances.
- Device Registry — keeps device metadata and capabilities.
- Command Scheduler — accepts scheduled commands from `Core` and routes them to adapters.
- Buttplug Client Manager — encapsulates `Buttplug.Client` usage and device event handling.
- GUI Layer (WPF) — thin client using the host API.
- Headless Mode — CLI entrypoint wiring to the same service layer.

Security model
- Local-only API by default; require explicit opt-in to expose to network.
- Log only non-sensitive metadata; avoid storing API keys in plaintext in defaults.
