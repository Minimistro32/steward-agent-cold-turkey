# Alpha.8 current macOS layout

Double-click the installed app to open its Terminal menu. All mutable data is in ~/Library/Application Support/Steward Cold Turkey, with imports/ and logs/ subdirectories. Use Open data folder. Migrate by uninstalling old login startup, stopping the old agent, and choosing Import existing config with its old config.json; this preserves IDs without overwriting existing settings. Existing CT blocks need no reimport. Developer ID signing and DMG instructions are in packaging/SIGNING-AND-INSTALL.txt and packaging/INSTALL.txt. These rules supersede historical executable-local macOS instructions below; Windows remains executable-local.

# Resource setup — alpha.5

The upcoming release opens a menu with no arguments or via the double-click launcher. Choose Connection settings first. `./Steward.ColdTurkeyAgent setup` remains a direct resource-setup shortcut. This revision is source-only while builds are paused. Setup always reads/writes `config.json` next to the executable, regardless of the terminal's working directory. The folder must be writable. Do not supply old `--config`, `--directory` or `--output` options.

## Connection and identity

On the first run, enter a host/IP only, choose TLS, enter the port (1883 without TLS, 8883 with TLS), and optionally supply username/password. A combined host:port or URL is rejected with an example showing the two separate fields. Setup generates `Cold-Turkey-[device-name]` and a UUID instance ID, then saves them with connection settings. Subsequent setup runs reuse the same file; edit it directly to change settings. No resource mappings or setup confirmation flags are saved.

Press Enter at the resource menu to finish connection setup without invoking CT. `mqtt-check` can then test the connection independently. It uses the configured client ID, so stop any running copy first. It does not register the agent; use `run` and inspect Steward for that.

## Create and finish a resource

Choose **1) Create a resource import**. Enter one resource name, whole-minute allowances (1–1440), websites, application files/folders/identifiers, and exceptions. Pasted HTTP(S) URLs have their scheme and trailing slashes removed: https://example.com/path/ becomes example.com/path. Wildcards and internal paths are retained; special exceptions such as file://*.* are unchanged. The same cleanup applies to manifest input and duplicate normalized entries are collapsed. The default `file://*.*` exception is shown in the prompt and included automatically; enter only additional exceptions. Application entries retain target-machine paths; they are not translated between macOS and Windows.

Setup reads the actual CT block list before generation to reject name collisions. It writes one file beside config.json: `§teward-[resource name]-import.ctbbl`. Existing files are not overwritten; if an earlier unused import is still there, remove that disposable file or choose another name. Resource names must also be valid portable filenames.

1. Import the file in Cold Turkey.
2. Manually set `§teward-[resource name]` autostart to **Time of Day, 03:30, every day**. The import alone does not establish this setting.
3. Keep the allowance presets disabled. Confirm their daily refill at **03:45**.
4. Confirm in the terminal. Setup rediscovers the selected group, checks all its preset statuses, and starts the baseline only if disabled.

All generated blocks share identical websites/apps/exceptions, are unlocked, have all-user scope and start disabled. Allowance names use four digits (`-0001`, `-0005`, `-0030`, up to `-1440`). CT passwords remain deferred.

Declining confirmation does not change blocks. If interrupted, import the file when ready and rerun setup with **2) Finish an existing imported resource**. Select the group from CT and confirm the manual settings. This also works for existing alpha.2 groups such as livetest. An already enabled baseline is not cycled. An enabled preset, incomplete import or failed CLI operation stops completion; there is no staged config to reconcile or overwrite.

**After changing Steward-managed blocks in Cold Turkey, restart the agent for Steward to recognize the changes.** Stop the agent before setup. The config itself is exclusively locked while setup/run is active. Import files can be deleted after import; only config.json is required agent state.

## Discovery rules

The observed block-list format has `Website & App Blocks` and `Device Blocks` headings with names on separate lines. ANSI color/bold formatting is stripped from headings, including in redirected CLI output. Blank lines are ignored. Only exact, case-sensitive `§teward-` website/app names are candidates; ordinary and device blocks are untouched.

A baseline needs at least one matching `-[minutes]` child. Legacy unpadded and new four-digit suffixes are accepted, with minutes from 1–1440. A resource name may end in digits when the group is unambiguous (for example `§teward-Study-15` and `§teward-Study-15-0005`). If a name could serve as both a baseline and another baseline's preset, the whole connected group is rejected. Duplicate minute choices such as `-5` and `-0005` are also rejected. Do not rely on block-list order.

Incomplete groups are reported and skipped. Resource IDs are derived from names using the previous generator's lowercase/slug rules; collisions cause both groups to be skipped. Renaming a baseline/resource changes its Steward identity. No valid resources means startup stops before registration; an unknown list format or duplicate exact name also fails discovery. Only a validated startup inventory is used until restart, including during MQTT refresh/reconnect.

## Diagnostics and file-based generation

`doctor` prints actual list/status output and accepts Enabled/Disabled or true/false tokens, ignoring case. Unknown output is rejected; no configurable parsing expressions are provided. Status does not prove remaining usage, target correctness or autostart configuration.

For repeatable generation, use [the resource manifest example](../examples/generator-manifest.example.json):

```sh
./Steward.ColdTurkeyAgent generate-presets --manifest /path/to/resource.json
```

Create config.json with setup first. This command uses the same CT collision checks and import naming, then tells you to finish through setup. It creates no manifest/session copy. See [Mac validation](mac-test-procedure.md).


## Adding time options to an existing resource

Stop the background agent and choose **Add allowance options** from the main menu (or run `add-allowances`). Select a discovered resource; its existing minutes are displayed. Enter only new minute values, then supply the path to a current CT export containing the exact baseline name. Exported website/app/exception strings are preserved, including any user edits. The default exception and URL normalization for new resources are not retroactively applied to existing resource targets.

The export must contain a continuous baseline with no allowance and all-user scope. If existing sibling presets are included, their targets must match the baseline. Duplicate/missing block names, invalid target arrays, custom user scopes, duplicate minute values and ambiguous resulting names are rejected. Only new disabled/unlocked presets with 03:45 refill are included in the output. Existing CT blocks and their enabled states are not modified.

Import the baseline-named `-allowances-import.ctbbl`, confirm that new presets are disabled, then start the agent. Repeated exports use a numbered filename without overwriting earlier imports. If interrupted after generation, import the generated file, leave new presets disabled and restart; there is no session to recover.

## Application identifier format

For **Application identifier**, enter `Bfxr.app:com.increpare.bfxr` without quotes or the `app:` prefix. Setup adds the prefix, producing `app:Bfxr.app:com.increpare.bfxr`. File manifests must include that full prefix. The bundle identifier must be the actual one used by the installed app.

## Menu and background operation

The main menu separates connection configuration, resource creation, adding allowances, finishing an import, background start/stop/restart, and install/uninstall login startup. Invalid actions return an error and the menu stays open. Exit closes the menu, not the agent. Diagnostics are available in the menu and as commands: `list-resources`, `doctor`, `mqtt-check` (alias `mqtt-test`). List resources is a fresh read-only CT scan; restart the agent to refresh its cached inventory. Stop the running agent before MQTT testing, which uses the exclusively locked config.

Stop the running background process before changing config or generating/completing resources. Connection edits preserve agentId and instanceId; Enter keeps displayed values, and `-` in the username field removes authentication. `start` and `restart` use the installed OS startup entry. Stop requests graceful cancellation, allows CT recovery to finish, and leaves login startup installed. For older startup registrations, uninstall with the old build before upgrading and reinstalling. Keep the folder in its final location.
