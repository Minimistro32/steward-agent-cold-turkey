# Findings and implementation record

- Read-only inspection of the supplied macOS binary established embedded CLI help and import/export references. This did not constitute execution on a Mac.
- The user verified baseline precedence, autonomous daily restoration including sleep/logout, allowance reset by off/on cycling, and safe ordering at renewal.
- The subsequent working `Sample.ctbbl` establishes the shared Windows/macOS export structure and the 03:30 baseline / 03:45 refill profile. No template exports or roundtrip confirmations are required for setup.
- The implemented workflow configures one resource at a time. Users import the generated file and manually set daily Time of Day autostart before confirming. Setup starts only the new baseline and preserves existing agent identity/resources.
- Password locking is deferred at the user's direction. Future password management is intended to come from the main Steward server.
- Local fake-CLI, grant, setup and MQTT checks pass; see [verification](verification.md). Windows runtime remains unverified, and real macOS CLI/login checks require the installation.

Use [current setup instructions](setup.md). The attached commercial binary, personal export, generated configurations and experimental tests remain untracked.

## User test — 2026-11-04

The generated import worked and setup activated the baseline after confirmation on the user's Mac. The agent connection did not appear to work, although MQTT Explorer connects; host/port input is a suspected cause, not a finding. Further testing is deferred. The next redesign should remove per-resource folders and setup.json, minimize agent.json, and discover resources/presets from CT's block list. See [next-session handoff](next-session.md).


## Alpha.3 revision — 2026-11-14

The requested simplification is implemented: executable-local config.json, CT-discovered inventory, baseline-named imports with four-digit allowance suffixes, stateless setup completion and independent MQTT diagnostics. See [verification](../verification.md) and [next-session handoff](next-session.md). Local regression/integration checks and the Apple Silicon publish pass; the fresh actual Mac connection/grant test is next.
