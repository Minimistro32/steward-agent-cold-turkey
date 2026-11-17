# Alpha.8 acceptance checks

1. Developer ID Application certificate with private key in signing Mac's Keychain. Sign Steward.command must fail if missing, never use ad-hoc. Verify correct authority/team, runtime flag, allow-jit entitlement and strict/deep signature.
2. Create DMG.command; open image, copy app to Applications or ~/Applications, eject image. Never install startup from the image. Check app signature after copying.
3. Confirm Steward icon in Finder. Double-click app: one Terminal menu opens; no app/menu recursion. Closing it does not stop installed background service.
4. Uninstall alpha.7 login entry with old build first. Stop old agent and import old config. Verify IDs/credentials preserved, original retained, repeated import refused, config outside app. Open data folder should show Finder at Application Support. Test fresh setup too.
5. Create/add CT resource imports under imports/. Stop before settings/resource changes. Reconfirm baseline/settings process.
6. Install startup from permanent app location. Check logs in data folder for correct executable/config/version, MQTT registration, grant and recovery. Test with Terminal closed, restart, logout/login. Startup must not open Terminal.
7. Stop, replace app at same path, verify data remains. If moved, uninstall/reinstall startup. Check repeat signing/updates for permission behavior; do not assume grant durability.
8. DMG and Developer ID do not replace notarization; record any Gatekeeper approval behavior separately.

# Alpha.6 bundle test (current)

1. Uninstall old login startup and stop any foreground agent.
2. Extract alpha.6 into a permanent writable location; copy config.json beside the app.
3. Run Sign Steward.command on macOS. Verify success and org.steward.coldturkey.agent identity. No sudo/certificate required.
4. Open Steward.command; inspect List resources and Doctor. MQTT test is terminal-only, not proof of background access.
5. Install login startup. Allow Local Network if prompted. Inspect logs/agent.log for the alpha.6 bundled executable path, correct config path, MQTT connected and Ready for grants.
6. Close Terminal and request a grant. Check correct CT switching and completed response.
7. Test graceful stop/start/restart and logout/login. Confirm config and CT inventory are preserved, and no mutable files appear inside the app.
8. If it fails, capture startup/network log and signing verification/LaunchAgent plist. Do not repeatedly re-sign, change UUID, or reset permissions in the same experiment.

Notarization and Developer ID are deferred. Ad-hoc signing itself and real launchd networking must be tested on a Mac; Linux checks cannot prove them. Leave duplicate privacy entries alone.

# Fresh Mac test — 0.1.0-alpha.4

Extract the new Apple Silicon release into a fresh writable folder and open Terminal there. You may copy config.json from alpha.3 to keep identity and connection settings; do not copy alpha.2 agent.json or setup.json. Existing CT blocks can stay in place.

1. Run `./Steward.ColdTurkeyAgent setup`. Enter host `192.168.50.60`, TLS `n`, port `1883`, and leave username blank if the broker has no authentication. IDs are generated without questions.
2. To reuse the existing livetest group, choose **2) Finish an existing imported resource**, select livetest, and confirm its 03:30 daily baseline autostart and disabled presets/03:45 refill. To test a fresh import instead, choose **1)** with a new name and harmless targets. Its baseline-named `-import.ctbbl` appears beside config.json, with padded allowance names.
3. Run `./Steward.ColdTurkeyAgent mqtt-check`. Expected: connection succeeded. This step needs no CT setup and does not register or change blocks. On failure, share the diagnostic output, not credentials.
4. Run `./Steward.ColdTurkeyAgent doctor` if needed to inspect discovered groups/statuses. livetest should have 1/5/30; test and testload should be reported incomplete and left alone.
5. Run `./Steward.ColdTurkeyAgent run`. Confirm connection/subscription/registration logs, then confirm the agent/device/resources appear in Steward. Registration publication alone does not establish server acceptance.
6. Select the new agent/resource in the appropriate Steward ward/policy. Request one minute. Expect selectedMinutes=1 and baseline on → all presets off → selected preset on → baseline off. Check access to the selected targets.
7. Stop the agent with Ctrl+C and verify CT exhausts the allowance independently. Restart and verify it does not cycle blocks. A fresh grant should reset its selected preset with the same baseline-first ordering.
8. Test broker disconnect/reconnect and confirm registration returns without CT state changes. After changing managed blocks, restart the agent before expecting Steward to recognize them.

Login startup and Windows runtime require separate later checks. No template export or import roundtrip is required. Do not repeat accepted CT behavior experiments unnecessarily. Keep generated files, credentials and local experiments out of tracked source.


## Upcoming menu/lifecycle revision — not yet built

After the build pause is lifted and a new package is validated:

1. Copy the existing config.json into the new permanent release folder. If an older login entry is installed, remove it using the old build first.
2. Double-click Open Steward.command. Confirm the menu opens even from a folder with spaces, and diagnostics are not menu options.
3. Check that connection edits retain IDs, URL input is cleaned visibly, default file://*.* is included, and application identifiers receive the correct prefix once.
4. Stop any foreground/background agent. Export a resource baseline from CT and use Add allowance options. Inspect the generated file: only new padded presets, same exported targets, 03:45 refill, disabled. Import and confirm; verify existing allowance/baseline states did not change.
5. Install login startup from the menu. Confirm the agent starts and logs to logs/agent.log. Close the menu; confirm the agent stays connected. Reopen the menu and Stop; confirm login registration remains but process exits.
6. Start and Restart; confirm only one instance runs and ordinary restarts preserve CT state. During a controlled grant transition, Stop must wait for recovery rather than force-killing. Review concise logs and response publication.
7. Log out/in and verify the process starts. Uninstall and verify it stops and does not return on login. Keep Windows checks separate.

Do not distribute the stale URL-only alpha.5 publish. The future packaging script publishes fresh output and adds the correct platform launcher.
