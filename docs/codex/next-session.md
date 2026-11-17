# Active handoff — alpha.8, updated 2026-11-17

## Latest Mac signing result — 2026-11-17

Developer ID signing and strict/deep verification now succeeded on the user’s Mac, with the expected identity/team/identifier and secure timestamp. The packaged script first failed with a missing timestamp, including on hotspot; a small executable probe and then manual verbose app signing by certificate name succeeded. Cause remains unisolated; do not claim fingerprint selection was broken or an Apple outage was proved. See [signing troubleshooting](../signing-troubleshooting.md) for the exact successful command and evidence for future releases. Signing script is unchanged. DMG creation, installed launch/runtime and background connectivity still await user validation; notarization deferred. This update supersedes older statements that certificate installation and Mac signing are outstanding.

## TODO: stale loaded login registration and timeout reporting

Recorded 2026-11-17; requested follow-up, not implemented.

Evidence: alpha.8 menu on Desktop reported “Input ended; returning to menu” on Start. Stop reported already stopped; the new Application Support agent.log did not exist. The plist on disk pointed to the Desktop app and Application Support logs, but `launchctl print gui/501/org.steward.coldturkey.6f61b75243e7535a` showed the loaded alpha.7 Downloads executable and old log directory, state “spawn scheduled”, last exit code 78 (EX_CONFIG). The mismatch is confirmed; whether old uninstall was skipped, failed or timed out is unknown. Do not attribute it to user error. Manual bootout of that service followed by bootstrap of the current plist was suggested; successful recovery has not yet been reported.

- [ ] Compare the actual loaded launchd job with the intended installation, not just the plist on disk, during start/install/upgrade.
- [ ] Detect stale registrations and provide a clear, safe recovery path. Preserve cooperative shutdown/in-flight recovery; do not replace a running old agent blindly.
- [ ] Verify uninstall actually unloads the job, and verify installation loads the intended executable and arguments before reporting success.
- [ ] Distinguish login-management timeouts from setup input EOF and user cancellation. LoginStartup.ExecuteAsync has a 15-second deadline; MainMenu currently labels non-user OperationCanceledException as “Input ended”. A launchctl timeout is suspected here, not directly proved. Report the failed operation and useful diagnostics.
- [ ] Add regression coverage for an old loaded job with a new plist, failed/timed-out unload, and successful upgrade, including alpha.7 executable-local data to alpha.8 Application Support migration.

## Future release process

User also requested a discussion of current macOS industry/Apple best practices and standardization for future updates. Before the next release, review signing/key custody, timestamp diagnostics/retries, notarization/stapling, DMG verification and a repeatable install/upgrade test; then document and automate the agreed process. See [release-process follow-up](../signing-troubleshooting.md#follow-up-standardize-future-releases). Notarization remains deferred; no new distribution/update mechanism has been selected.

## Deferred password design

See [server-managed agent secrets and locked agents](../server-managed-resource-credentials.md). Agreed direction: one stable configurable secret per agent (all its Steward-managed CT blocks share it), with server-owned locked-agent policy. Secret/protected-device administration requires an admin who uses none of that agent’s devices; preserve at least one eligible admin across account, assignment and lifecycle changes and prevent self-association removal from bypassing eligibility. Agent advertises secret support/label; server owns configured/locked state; local application status is separate. Require the eligible admin to confirm an external password copy before locking/changing it, retain old/new versions through uncertain transitions, and document server-independent recovery. This supersedes per-resource credentials and optional independent administration. Generic design only; field names, transfer/recovery flows and CT feasibility remain open. No implementation or protocol changes; revisit after alpha.8 Mac validation.


User confirmed alpha.7 background MQTT connects. Now requested icon matching /root/steward/frontend/steward-ui/public/steward-logo.svg, Finder double-click opens Terminal CLI, Application Support data with Open data folder, DMG drag-to-Applications and Developer ID signing. User has paid Apple account but certificate/private key not installed or unsure; explained website CSR/certificate setup. Never ask for private key upload. Notarization remains deferred. No subagents.

Implemented: ApplicationPaths on macOS uses ~/Library/Application Support/Steward Cold Turkey; imports in imports/, logs in logs/. Per-user data shared across app copies. SaveNew creates directories and restrictive config permissions. Menu adds Open data folder and Import existing config; import validates/preserves IDs, refuses destination overwrite, respects source lock and leaves original intact. Alpha.7 migration requires uninstall old startup and import old config explicitly; no automatic search through Downloads.

Finder no-args (or legacy -psn launch arg) opens signed Resources/Open Menu.command via /usr/bin/open -a Terminal; that runs explicit menu to avoid recursion. Background still invokes the native exe with background argument. Info.plist uses LSUIElement, stable bundle ID and Steward.icns. Supplied SVG copied into packaging; icns/PNG rendered via temporary @resvg/resvg-js install, no project dependency changes. The attempted Python venv lacked ensurepip; Node rendering succeeded. Icon Composer not required.

Sign Steward.command now strictly requires valid Developer ID Application identity/private key from user's Keychain; list/select identities, fail without one (no ad-hoc fallback). Native components signed before bundle; hardened runtime + allow-jit only, timestamp, verify authority/team/identifier. Local Mac signing is outstanding. Create DMG.command verifies the Developer ID app, stages with ditto plus /Applications symlink and INSTALL.txt, creates/verifies UDZO and checksum, refuses overwrite. Final signing/DMG are Mac-side, not claimable from Linux. No notarization. Users of final DMG should not sign it themselves.

Source build and core/CLI/wire/background checks passed; core includes path independence, config import preservation/no overwrite/locked source, and menu additions. Shell/Python syntax and diff checks passed. Package/archive inspection still to be recorded below. Need real Mac tests for icon, Finder menu, signature with certificate, hardened .NET startup, DMG, config import, background connectivity with Terminal closed, and login/restart. If signing fails, preserve exact evidence; do not silently revert to ad-hoc. Prior alpha.7 success is not proof alpha.8 works.

Repository /root/steward-agent-cold-turkey, sibling server/frontend /root/steward. Preexisting untracked src/packaging/examples/docs and docs/results.md deletion preserved; tests/artifacts ignored. No commits or server/frontend changes. Work still requires escalated filesystem writes.

---

# Active update — alpha.6 bundle test, 2026-11-16

This section supersedes earlier alpha.5 and Developer ID planning notes below.

## Revised plan and implementation

User chose local ad-hoc signing, no certificate setup, notarization or sudo. Package a self-contained macOS app with stable org.steward.coldturkey.agent identity, Local Network usage description, background-only metadata, and explicit LaunchAgent AssociatedBundleIdentifiers. Keep the native UUID unchanged to test bundle attribution separately. No claim that this fixes macOS until device tests pass.

Implemented alpha.6: ApplicationPaths resolves config/import/log data to the release folder beside Steward Cold Turkey.app. Normal/Windows layouts remain executable-local. Open Steward.command verifies the bundle before opening its menu. Sign Steward.command runs on the user's Mac, refuses a running installation, ad-hoc signs all native runtime files then the outer app, and verifies the sealed result. It intentionally does NOT enable hardened runtime for this local ad-hoc experiment; no certificate, entitlement expansion, notarization, or library-validation bypass is introduced. Signing requires native macOS tools and is untested here on Linux. No persistent signing marker is created.

Menu includes List resources / mqtt-test / doctor. Login registration verifies bundle association on start; uninstall remains possible for older associations. Startup logs identify version, mode, executable and config. Existing config IDs, CT blocks and server protocol stay unchanged.

## User test and acceptance

Uninstall old login entry from old folder; stop foreground process. Extract alpha.6 into permanent writable folder. Copy existing config.json beside app. Double-click Sign Steward.command; only continue when verification succeeds. Double-click Open Steward.command; install login startup; approve Local Network if prompted. Check logs/agent.log for MQTT connected / Ready for grants. Test grant with Terminal closed, stop/start/restart and logout/login. Do not alter or re-sign a running bundle. Leave duplicate privacy entries untouched. Upgrade permission persistence is unproven with ad-hoc signing.

Evidence so far: alpha.5 full foreground run connects; background HostUnreachable persists after toggle/reinstall. Re-signing bare executable with custom identifier failed and created duplicate permission entry. System logs unavailable without sudo. User has developer account but deferred certificates. OpenCode issue 58080 provides a reported successful ad-hoc bundle experiment, not proof for this agent. Next fallback if bundle fails: preserve logs/signature/registration evidence before testing a distinct native UUID or other causes.

---

# Cold Turkey agent — active handoff

Updated 2026-11-16 after building and packaging alpha.5 for user testing.

## Current release and next step

On 2026-11-16 the user explicitly authorized a new macOS test package, lifting the build/package pause. Fresh alpha.5 Apple Silicon archive and SHA-256 file are in artifacts/steward-cold-turkey-0.1.0-alpha.5-osx-arm64.tar.gz[.sha256]. The packaging script performed a fresh publish; stale preliminary output was not used.

Build passed with zero warnings/errors. CoreChecks, cli_checks.py, wire_checks.py and background_checks.py all passed against rebuilt binaries. Archive checksum, executable/launcher permissions, START-HERE.txt and exclusion of user config/import/log files passed inspection. Archive is 30.8 MiB. Actual macOS launchctl/launcher and CT import validation remain the next user tests; Windows runtime remains unverified.

## Implemented in source

- Executable-local config.json remains the only required persistent application state: device-based editable agentId, persistent UUID instanceId, host/port/TLS and optional credentials. Connection edits preserve IDs. No resource inventory/session/duplicate-history files.
- No arguments (or menu) opens connection settings, create resource, add allowances, finish import, background start/stop/restart, login install/uninstall, and exit. Diagnostics remain argument-only: doctor and mqtt-check/mqtt-test. Errors return to menu; closing menu leaves background agent running.
- Setup has spacing and section labels, cleaned HTTP(S) targets/trailing slashes, visible default file://*.* exception for NEW resources, and application identifier instructions: enter Bfxr.app:com.increpare.bfxr; setup adds app:. Manifest uses the full app: prefix.
- Add allowances selects an existing discovered resource and CURRENT CT export, validates baseline/compatible scope and included sibling targets, preserves exported target strings/exceptions, and generates ONLY new disabled/unlocked padded presets with 03:45 refill. No baseline/existing preset overwrite or activation. Export users must be all with no custom users. Import confirmation checks new presets without cycling old blocks.
- New-resource import still requires manual daily baseline 03:30 autostart and preset 03:45 refill confirmation. Imports sit beside config, named after baseline with import in filename; additions use allowances-import. CT changes require agent restart.
- Readable Unicode logs, HH:mm:ss local timestamps, startup date/timezone, grouped grants, plain response outcomes, retained request IDs/errors. MQTT response publication/protocol unchanged.
- BackgroundControl uses transient current-user-only named pipes keyed by full config path. Dedicated background command serves status/stop; cancellation waits for existing recovery and process teardown. No force-kill of the agent. Config retains FileShare.None for reliable exclusivity; controls address the pipe without reading locked config. This supersedes earlier notes proposing shared-read locking.
- LoginStartup supports graceful start/stop/restart without removing registration, verifies installed executable/arguments, waits for startup/shutdown, and stops before uninstall. macOS uses bootstrap or kickstart without -k; Windows uses scheduled task Run. Install starts now and at login. No automatic crash restart. Old alpha.4 startup must be uninstalled with its old build before installing these controls; uninstall before moving folders/changing agentId.
- Packaging source adds Open Steward.command (executable) and Open Steward.cmd plus generated START-HERE.txt. scripts/package_release.py always publishes fresh, adds launcher, refuses existing release overwrite/config/import/log leakage, and creates archive/checksum. Script successfully produced the fresh alpha.5 package after authorization.
- README, setup, verification and Mac testing documents updated. Examples include app identifier syntax.

## Validation and next work

All local core/CLI/wire/background checks passed on 2026-11-16, including separate-process cooperative stop, recovery wait, config preservation and idempotent stop. These checks use test fixtures and do not establish actual macOS/Windows startup-manager behavior.

Actual Mac test: uninstall any old alpha.4 login entry using its old build; extract alpha.5 into a permanent writable folder; copy existing config.json; double-click Open Steward.command. Test stop/change/start, adding allowances from a current export, install/start/stop/restart/uninstall, grants/recovery/logs and persistence across login. Existing resources need not be reimported. Verify macOS launchctl behavior and named-pipe permissions. Windows runtime/scheduled-task/launcher validation remains outstanding.

## Observed evidence and boundaries

User alpha.4 logs establish successful setup/import/baseline activation, MQTT connection/subscription/registration publication, real grant receipt, verified CT preset/baseline switching, and accepted/completed response publication attempts. Server receipt and actual allowance exhaustion are not established. Earlier alpha.4 checks passed before the pause; they do not validate this new source.

Server MqttMessageDispatcher validates/logs responses, without persisting them or updating UI request status. Registration acceptance is not acknowledged. No server/frontend/protocol changes requested or made. CT passwords, consumption accounting and durable history remain out of scope.

Existing ANSI heading handling and exact §teward- website/app discovery remain. New suffixes use four-digit minutes; legacy unpadded suffixes accepted with ambiguity checks. User ordinary steward-01/05/etc. blocks were sorting experiments; leave untouched.

Sample export attachment: /root/.codex/attachments/3396f774-d812-4cfe-ae12-0a45a3cfb0d2/Sample.ctbbl. Use only as structural evidence, not a block to install; private targets were not copied into source.

## Workspace

Repository: /root/steward-agent-cold-turkey. Sibling server: /root/steward. Sandbox is read-only; source writes require escalated approval. No subagents authorized. Existing working tree has untracked src/examples/docs, ignored tests/artifacts, moved docs under docs/codex, and a preexisting docs/results.md deletion. Preserve all; no unrelated reset/commit. Source implementation spans AgentCommands, MainMenu, BackgroundControl, LoginStartup, SetupWorkflow/Prompts, PresetGenerator, logging, packaging and docs. Keep this handoff current.

## Follow-up 2026-11-16 — diagnostics menu and networking report

User now wants diagnostics in the menu. Implemented options 10 List resources, 11 mqtt-test, 12 doctor, keeping existing numbering. list-resources also works directly without config and shows fresh read-only CT discovery, baseline/preset names, minutes, skipped-group warnings, empty state and restart reminder. Core display/menu regressions and CLI checks passed; build zero warnings/errors. Source changes after alpha.5 have NOT been packaged. Alpha.5 archive remains unchanged.

User supplied log shows HostUnreachable repeatedly from background both before and after successful Terminal mqtt-test. Likely macOS Local Network privacy difference (Apple TN3179 documents Terminal exemption versus launch agents); not confirmed. Added conditional macOS reachability guidance and clarified mqtt-test only tests its own process, not background access or agent.log. Next diagnostic: allow agent in Local Network if listed, restart, compare stopped-background/foreground run. Missing privacy entry may require future signed/bundled identity work; no arbitrary identity or permission bypass introduced. MQTT testing retains config exclusivity, so stop agent first; read-only list/doctor don't require config.


## Alpha.6 validation — 2026-11-16

Build passed with zero warnings/errors. CoreChecks (including external bundle data paths and LaunchAgent association), CLI, MQTT-wire and background IPC regression checks passed. Shell/Python syntax and git diff checks passed. Fresh osx-arm64 alpha.6 archive is 30.8 MiB; checksum, metadata, executable script permissions, absence of user state and unchanged native UUID verified. Package: artifacts/steward-cold-turkey-0.1.0-alpha.6-osx-arm64.tar.gz with .sha256 companion. macOS codesign and actual background network success remain unverified and must be tested on the user's Mac. The archive is prepared for local signing, not already bundle-signed or notarized.


## Alpha.7 correction — 2026-11-16

User alpha.6 signing failed while signing the main executable: codesign treated its containing bundle as a unit and rejected unsigned System.Net.WebSockets.Client.dll under Contents/MacOS. Alpha.7 uses macOS single-file publish with no debug symbols, removing loose managed assemblies from the native code directory. Sign script skips the main executable in the component loop, then signs it as part of the final bundle. Packager rejects loose DLL/PDB files. Windows unchanged. Actual produced MacOS directory contains only the bundled executable. Native UUID is not explicitly rewritten.

CoreChecks/build passed. Publish succeeded with IL3000 warning for Assembly.Location in the dotnet-host-only invocation branch; packaged apphost never takes that branch. Archive checksum, absence of loose DLL/PDB/user data, embedded payload size, permissions and version0.1.7 verified. Shell syntax passed. macOS signing and networking remain unverified until user test. Package artifacts/steward-cold-turkey-0.1.0-alpha.7-osx-arm64.tar.gz plus checksum. Use fresh extraction; do not reuse partially signed alpha.6. Run Sign Steward.command, then Open Steward.command only after verification. Create config via Connection settings because user's old config is lost.


## Alpha.8 packaging verified — 2026-11-16

Fresh publish/archive completed. Preparation archive: artifacts/steward-cold-turkey-0.1.0-alpha.8-osx-arm64.tar.gz (28.9 MiB), with .sha256 companion. Verified SHA-256; plist bundle ID/version0.1.8, LSUIElement and icon; icon chunk integrity; executable script/app permissions; exact archived signing/DMG/menu scripts and instructions matching source; allow-jit-only entitlement; no config/import/log data, PDB or loose managed DLLs. Shell syntax and git diff checks passed. Earlier alpha.8 core/CLI/MQTT-wire/background-stop regressions passed.

Outstanding is exclusively Mac-side: certificate installation, Developer ID signing with private key, actual DMG creation, Finder menu/icon, hardened-runtime startup and background MQTT tests after install/data migration. No claim of a signed/notarized archive or existing DMG. User asked website certificate/CSR directions and email visibility; explained CSR goes to Apple and signed certificate is public, inspect issued fields rather than promise email omission. Private key must stay on user's Mac. No additional source changes currently required.
