# Steward Cold Turkey Agent — alpha.8

A per-user Cold Turkey agent for Steward. macOS alpha.8 includes the Steward icon, a double-click app that opens the Terminal menu, and a persistent data folder outside the signed app. The alpha.7 background connection succeeded on the user's Mac; alpha.8 Developer ID signing and Finder/DMG behavior still require Mac validation.

## macOS signing and installation

The generated archive is **preparation output, not a signed distribution**. Complete these steps on the Mac holding your Developer ID Application certificate and private key:

1. Extract the archive and read `SIGNING-AND-INSTALL.txt`.
2. Run `Sign Steward.command`. It requires a valid Developer ID Application identity, signs with hardened runtime and the .NET JIT entitlement, timestamps the signature, and verifies the certificate/team and bundle. There is no ad-hoc fallback.
3. Run `Create DMG.command` to create the drag-to-Applications image and checksum from the verified app. The contained app is signed; notarization is deferred.
4. Uninstall old login startup from its old release before migrating alpha.7 or earlier. Stop any foreground run and retain the old config.json.
5. Open the DMG, copy the app to `/Applications` (or `~/Applications` if needed), eject the image, and double-click the installed app.
6. Choose **Import existing config** to preserve IDs/settings, or **Connection settings** to start fresh. Then **Install login startup**.

Users of the finished DMG do not need signing certificates. Developer ID signing alone does not guarantee Gatekeeper approval without notarization. Never edit bundle contents after signing. No Icon Composer project is needed for the included SVG-derived `.icns`.

Certificate directions: [Apple Developer ID](https://developer.apple.com/help/account/certificates/create-developer-id-certificates), [certificate signing request](https://developer.apple.com/help/account/certificates/create-a-certificate-signing-request). The private key stays on the signing Mac.

## Data and configuration

macOS uses `~/Library/Application Support/Steward Cold Turkey/` containing `config.json`, `logs/`, and `imports/`. Choose **Open data folder** to open it in Finder. Data is per user and shared by copies of the app, survives app replacement, and is never written into the signed bundle. Config imports preserve agentId and instanceId, keep the source, and refuse to overwrite an existing config. Stop the old agent before importing its locked config.

Windows/source runs outside macOS retain executable-local config and imports. On macOS all runs use Application Support. Config contains device-derived editable agentId, a persistent generated instanceId UUID, host, port, tls, and optional username/password. Connection settings retains IDs and offers saved values as defaults. Stop before editing config. No persistent resource inventory or duplicate-request history is stored.

Moving an installed app requires uninstalling login startup before moving and reinstalling afterward; the OS entry stores an absolute executable path. Data stays in Application Support. Moving to a new computer requires separate data transfer and review of agent identity to avoid conflicting registrations.

## Menu and commands

The menu offers connection settings, new managed resource, additional allowance options, finish import, background start/stop/restart, login install/uninstall, List resources, MQTT test, Doctor, Open data folder, and Import existing config. Closing the menu leaves the background agent running. Startup runs `background` explicitly and never opens Terminal.

Direct macOS command example:

```sh
"/Applications/Steward Cold Turkey.app/Contents/MacOS/Steward.ColdTurkeyAgent" run
```

Other arguments: `menu`, `setup`, `configure`, `create-resource`, `finish-resource`, `add-allowances`, `list-resources`, `doctor`, `mqtt-test` (alias `mqtt-check`), `open-data`, `import-config --file PATH`, `start`, `stop`, `restart`, `login-install`, `login-uninstall`, `generate-presets --manifest PATH`, `help`.

MQTT test only establishes its own process's broker connection, not background connectivity or server registration. Stop the running agent before MQTT testing because config is exclusively locked. Doctor and List resources are read-only and do not need config. List resources is a fresh CT scan; restart a running agent to refresh its cached inventory.

## Cold Turkey resources

Discovery accepts exact `§teward-` website/app groups, matching baseline plus valid allowance presets. New names use four-digit minutes, for example `§teward-Example-0005`. Legacy unpadded names are accepted when unambiguous; ordinary/device blocks are untouched. Alpha.4 fixed ANSI-colored headings.

Create a resource, import its baseline-named `.ctbbl`, set baseline autostart manually to 03:30 every day, confirm 03:45 preset refill and disabled presets, then finish setup. URLs lose HTTP(S) prefixes and trailing slashes; new resources include `file://*.*` by default. App identifier entry: `Bfxr.app:com.increpare.bfxr`; setup adds `app:` (manifest uses full prefix).

To add allowances, stop the agent and supply a current CT export of the chosen resource. Only new disabled presets are exported. Actual targets/exceptions are preserved, incompatible/custom scope is rejected, and baseline/existing presets are not overwritten or cycled. Restart after any CT changes. Imports are disposable.

## Build and validation

```sh
python3 scripts/package_release.py --runtime osx-arm64
```

Also supports `osx-x64` and `win-x64`. Always publishes fresh, refuses archive overwrite, and excludes config/import/log data. macOS managed assemblies are embedded to avoid codesign rejecting loose DLLs. Windows packaging remains unchanged. `scripts/build_icon.cjs` regenerates the checked-in `.icns` from the supplied SVG using `@resvg/resvg-js`; rendering is not needed for ordinary packaging.

Steward.Messaging remains pinned to 0.1.0-alpha.1. No server/protocol changes. Responses are published; server acceptance/usage exhaustion should not be inferred from local logs. Actual Windows runtime remains unverified. See docs/codex/next-session.md for current state and validation boundaries.
