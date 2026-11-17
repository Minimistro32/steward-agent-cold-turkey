# Verification status — 0.1.0-alpha.3

Local verification on 2026-11-14 used .NET 10 on Linux, an injected fake Cold Turkey CLI, and a local MQTT 3.1.1 peer. Experimental checks remain ignored/untracked under tests/. They are not substitutes for the next actual Mac test.

| Area | Result |
| --- | --- |
| Build/package | Debug build and self-contained Apple Silicon alpha.3 publish succeed without warnings |
| Config/identity | Five unauthenticated fields; stable generated IDs; optional username/password; owner-only Unix permissions; config beside assembly even from another working directory |
| Persistence | No staging, setup session, inventory, verification flag or sidecar lock files; config itself prevents concurrent runs |
| Host input | Host-only validation rejects combined host:port, URLs, paths and whitespace; IPv4/IPv6/DNS accepted |
| Discovery | User's actual sectioned Mac list recognized; unordered legacy names and four-digit presets accepted; unrelated/device blocks excluded |
| Discovery rejection | Incomplete groups, numeric role ambiguity, duplicate minute values, resource-ID collisions, invalid ranges, duplicate names and unknown list formats handled without CT mutations |
| Generator | Baseline-named import beside config; four-digit minutes; identical targets; 03:30/03:45 settings; filename/CT collisions rejected; no overwrites |
| Completion | Decline does not mutate; confirmation rediscovers group, checks presets and starts only disabled baseline; repeat does not cycle; incomplete import/enabled preset/CLI failure do not report success or change config |
| Grant regression | Selection boundaries, invalid targets, expired/duplicate/conflicting requests, required off/on reset, partial application and recovery pass |
| MQTT integration | Registration, refresh, reconnect, retained/malformed rejection, duplicate preservation across reconnect, failed-command/timeout recovery and process restart state preservation pass |
| MQTT rejection | Explicitly checks broker connection return code; denied authentication fails diagnostic and normal run retries instead of reporting false success |
| Standalone connection check | Real CLI succeeds against local peer without CT, publishes no registration/status, reports network/auth failures, rejects an untrusted TLS certificate and does not print credentials |
| Login metadata | Existing LaunchAgent/scheduled-task document checks pass with simplified invocation; actual login behavior remains unverified |

Commands used: `dotnet run --project tests/CoreChecks`, `python3 tests/wire_checks.py`, `python3 tests/cli_checks.py`. The older setup_checks.py is an alpha.2 experiment and is superseded by the alpha.3 core/setup checks.

## Actual Mac evidence

- The user previously imported generated blocks successfully and confirmed initial baseline activation after manual autostart confirmation.
- On 2026-11-14 the user supplied actual `-list-blocks` output and successful `Enabled`/`Disabled` status output for livetest and its 1/5/30 presets.
- The alpha.2 attachment had host:port in the host field and the staging configuration's verification flag was false. The user's run stopped at that flag before MQTT; setup completion then rejected a changed active configuration. These observed setup failures are removed in alpha.3, but a successful real MQTT connection is still to be established.
- User-verified baseline precedence, independent restoration, off/on allowance reset and safe renewal ordering remain accepted.

Next: fresh alpha.3 setup, real MQTT registration and grant/exhaustion/restart/reconnect checks in [the Mac procedure](mac-test-procedure.md). macOS login and Windows runtime remain separate pending checks. No server/frontend/protocol changes, CT password locking, persistent grant history, or consumption reporting were introduced.


## Alpha.4 parser fix — 2026-11-14

The user's alpha.3 doctor output exposed ANSI SGR sequences around the block-list headings (`ESC[1;32m` and `ESC[0m`). The terminal rendering had hidden those bytes in the earlier supplied list. Alpha.4 strips SGR formatting from headings only and keeps block names exact. Regression checks cover the exact redirected output, colored headings with valid managed groups and CRLF, and rejection of unknown colored headings.

The latest supplied list contains steward-01/05/10/30, not a complete §teward- resource group. Parsing that list now succeeds but produces no managed resources. Setup must create/import a managed baseline and presets before run can register resources. No existing blocks are renamed or adopted automatically.

The user clarified that the ordinary steward-* names were temporary alphabetical-sorting experiments, not intended managed resources. Leave these untouched.


## Alpha.5 URL cleanup

Interactive and manifest website/HTTP(S) exception inputs strip http:// or https:// and trailing slashes. Normalized duplicates collapse; internal paths, wildcards and file://*.* remain intact. Empty targets after cleanup are rejected. Regression checks cover these cases and the prompt feedback. The ANSI-heading fix is retained.


## Menu/lifecycle source revision — 2026-11-15

Source now includes the main menu, double-click launcher templates, fresh-build packaging script, default file exception, app-format guidance, export-based additional allowances, readable logs, connection editing and cooperative background start/stop/restart. The user supplied alpha.4 logs showing real MQTT grant reception and successful verified CT switching. This establishes more than the earlier setup-only evidence, but does not establish actual allowance exhaustion or server receipt of responses.

**No compilation, runtime tests, publish, or package creation has been performed for the menu/lifecycle revision**, per the user's continuing build pause. Only shell/Python syntax checks and source review have run. CoreChecks has new regression cases and background_checks.py is prepared to test IPC and delayed shutdown without installing an OS startup entry. Existing wire_checks.py and cli_checks.py must be rerun after a rebuild; existing binaries are stale. Actual macOS/Windows startup-manager validation is pending.


## 2026-11-16 — alpha.5 built and packaged

User lifted the build/package pause. Fresh build passed with zero warnings/errors. Rebuilt CoreChecks and CLI, MQTT wire, and background IPC checks all passed. The latter confirms cooperative shutdown waits for recovery/config unlock, is idempotent, preserves config and creates no state file. Fresh osx-arm64 publish/archive includes executable double-click launcher and START-HERE.txt. SHA-256, archive permissions, and absence of user config/import/log files were verified. These results supersede the source-only status above. Actual Mac launchctl, double-click, CT imports and Windows runtime still require device testing.


## Alpha.6 validation — 2026-11-16

Build passed with zero warnings/errors. CoreChecks (including external bundle data paths and LaunchAgent association), CLI, MQTT-wire and background IPC regression checks passed. Shell/Python syntax and git diff checks passed. Fresh osx-arm64 alpha.6 archive is 30.8 MiB; checksum, metadata, executable script permissions, absence of user state and unchanged native UUID verified. Package: artifacts/steward-cold-turkey-0.1.0-alpha.6-osx-arm64.tar.gz with .sha256 companion. macOS codesign and actual background network success remain unverified and must be tested on the user's Mac. The archive is prepared for local signing, not already bundle-signed or notarized.


## Alpha.7 correction — 2026-11-16

User alpha.6 signing failed while signing the main executable: codesign treated its containing bundle as a unit and rejected unsigned System.Net.WebSockets.Client.dll under Contents/MacOS. Alpha.7 uses macOS single-file publish with no debug symbols, removing loose managed assemblies from the native code directory. Sign script skips the main executable in the component loop, then signs it as part of the final bundle. Packager rejects loose DLL/PDB files. Windows unchanged. Actual produced MacOS directory contains only the bundled executable. Native UUID is not explicitly rewritten.

CoreChecks/build passed. Publish succeeded with IL3000 warning for Assembly.Location in the dotnet-host-only invocation branch; packaged apphost never takes that branch. Archive checksum, absence of loose DLL/PDB/user data, embedded payload size, permissions and version0.1.7 verified. Shell syntax passed. macOS signing and networking remain unverified until user test. Package artifacts/steward-cold-turkey-0.1.0-alpha.7-osx-arm64.tar.gz plus checksum. Use fresh extraction; do not reuse partially signed alpha.6. Run Sign Steward.command, then Open Steward.command only after verification. Create config via Connection settings because user's old config is lost.


## Alpha.8 packaging verified — 2026-11-16

Fresh publish/archive completed. Preparation archive: artifacts/steward-cold-turkey-0.1.0-alpha.8-osx-arm64.tar.gz (28.9 MiB), with .sha256 companion. Verified SHA-256; plist bundle ID/version0.1.8, LSUIElement and icon; icon chunk integrity; executable script/app permissions; exact archived signing/DMG/menu scripts and instructions matching source; allow-jit-only entitlement; no config/import/log data, PDB or loose managed DLLs. Shell syntax and git diff checks passed. Earlier alpha.8 core/CLI/MQTT-wire/background-stop regressions passed.

Outstanding is exclusively Mac-side: certificate installation, Developer ID signing with private key, actual DMG creation, Finder menu/icon, hardened-runtime startup and background MQTT tests after install/data migration. No claim of a signed/notarized archive or existing DMG. User asked website certificate/CSR directions and email visibility; explained CSR goes to Apple and signed certificate is public, inspect issued fields rather than promise email omission. Private key must stay on user's Mac. No additional source changes currently required.


## Alpha.8 Mac Developer ID signing confirmed — 2026-11-17

User-provided output confirms successful Developer ID Application signing, strict/deep verification, expected org.steward.coldturkey.agent identifier and RZ775S6QM6 team, hardened runtime, and Apple secure timestamp at 12:14:35 PM America/Boise. Initial script timestamp failures persisted on hotspot; a standalone signing probe and subsequent manual verbose signing by certificate name succeeded. Cause is not isolated. See [exact commands and troubleshooting record](signing-troubleshooting.md). DMG creation and installed runtime/background behavior remain unverified; no notarization or new package performed.
