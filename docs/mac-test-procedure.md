# Guided checks on the existing Mac

Nothing below has been executed by the assistant on the target Mac. The user has since verified four behavior gates; see [updated design](baseline-autostart.md). Do not repeat those checks unnecessarily. For every new grant, the updated baseline-first, explicit preset off/on transition supersedes the original transition below. Run one stage at a time and record results before proceeding. Do not automate the entire sequence. Keep CT's usual background components running throughout; stop only your own orchestration process for crash checks.

## 1. Establish the environment without changing blocks

Record macOS version, CT version/Pro status, browser/extension versions, account type, local timezone, and relevant statistics settings. Keep account identifiers private. In Terminal:

```sh
CT='/Applications/Cold Turkey Blocker.app/Contents/MacOS/Cold Turkey Blocker'
"$CT" -help
```

Compare local help with the reference. Record stdout, stderr, and exit status (immediately afterward: `printf '%s\n' "$?"`). Do not share `-list-blocks` output containing personal names. Keep the normal UI open to test coexistence. Stop if commands differ or unexpectedly affect existing blocks.

## 2. Establish a disposable export safely

Use a unique prefix such as `Steward-CT-Probe-20261103-X7Q`; check locally that it does not already exist. Create only website blocks targeting `example.com`, without exceptions, app targets, schedules, or locks initially. Use the intended account scope explicitly. Verify this site is not already covered by an existing block; if it is, stop and choose another harmless target without editing existing blocks.

Create a baseline with no allowance and disabled two- and three-minute allowance presets through the UI for this experiment. If those durations are unavailable, record and use the smallest supported values. CLI-only provisioning is not established.

Inspect the export UI before exporting. If it only exports all blocks, keep that export private and outside git; **do not upload it**. Report its extension and whether it is readable text, without contents. We must establish safe extraction of only disposable records before sharing. Do not guess which fields contain passwords or account information. A separate disposable installation/export may be needed if safe extraction is impossible; that is a discussion point, not authorization to alter deployment.

After receiving a safe sample, inspect schema/version, identity, targets, allowance units, locks, and account scope. First validate an unchanged disposable-only roundtrip and confirm the import UI offers a way to preserve unrelated blocks. Cancel any replace-all operation. Compare the unrelated blocks locally before/after; do not publish their data. Only then implement a generator writing a separate output file, with unique names and no credentials, and require explicit file selection for import. Validate generated targets, durations, lock representation, and account scope in the UI and re-export. A password-bearing export stays private.

## 3. Confirm exact CLI behavior on disposable blocks

Set these names to the exact names created above:

```sh
BASE='Steward-CT-Probe-20261103-X7Q-Baseline'
SHORT='Steward-CT-Probe-20261103-X7Q-02m'
OTHER='Steward-CT-Probe-20261103-X7Q-03m'
"$CT" -status "$BASE"
"$CT" -status "$SHORT"
```

For CLI creation, optionally create a fourth, unused disposable block:

```sh
"$CT" -add-block 'Steward-CT-Probe-20261103-X7Q-CLI'
"$CT" -add 'Steward-CT-Probe-20261103-X7Q-CLI' -web 'example.com'
```

Verify its target and scope in the UI before enabling it. Do not infer success from exit status alone. Compare `-start` versus `-start ... -as-is` only on these unlocked disposable blocks, recording whether allowance and configuration survive. Stop on any unexpected lock or target change.

## 4. Transition and interruption checks

Start the baseline and verify a fresh navigation is blocked:

```sh
"$CT" -start "$BASE" -as-is
```

Then start the allowance replacement while the baseline remains enabled:

```sh
"$CT" -start "$SHORT" -as-is
```

Pause here. Check both statuses, UI allowance/configuration, and fresh navigation. An enabled status alone cannot verify enforcement. If allowance overrides the baseline unexpectedly, record the overlap behavior and stop to evaluate it. Otherwise verify the replacement is configured correctly before stopping the baseline:

```sh
"$CT" -stop "$BASE"
```

Observe whether usage becomes available and then blocks at exhaustion. Repeat with interruption before replacement start, after replacement start, and after baseline stop. No persistent controller is needed for these manual single commands; leaving Terminal idle provides the absent-orchestrator case. Also test killing only an experimental controller, if one is later created. Never kill CT or its helpers. Test return to baseline by starting/verifying baseline before stopping the preset. Repeat for preset-to-preset switching, with both partially used and exhausted previous presets. Record countdown changes during overlap and possible extra credit from the replacement.

## 5. Usage and repeatability matrix

For each row record wall time, displayed usage before/after, enabled status, and fresh navigation result. Keep global statistics settings unchanged; record their effect instead.

| Check | Procedure / question |
| --- | --- |
| Foreground use | Interact with the test page through exhaustion; measure actual used time |
| Inactivity | Leave page foreground without input for over three minutes; compare countdown |
| Background tab | Keep test page behind an unrelated foreground tab, then inspect remaining time |
| Browser closed | Close browser, wait, reopen; does usage persist? |
| Sleep | Sleep and resume; does usage persist without wall-time consumption? |
| Logout/login | Log out and in with no orchestrator autostart; inspect allowance and enforcement |
| Same preset | Stop/restart partially used and exhausted presets; does either refill? |
| Alternate presets | Consume A, switch to B, then A; identify independent pools and reset behavior |
| Duplicate delivery | Issue start twice for an already enabled preset; any fresh allowance? |
| Agent absent | Leave CT running without controller until exhaustion; prove blocking resumes |

For repeat tests, restore the baseline between scenarios using the verified start-before-stop sequence. Record exact initial state; do not label a changed counter as consumption without accounting for resets.

## 6. Natural midnight

Use only the disposable target and leave the orchestrator absent. Exhaust a preset before local midnight, leave it enabled, and record navigation/counter immediately before and after midnight. Repeat another night with partially consumed allowance. Record timezone, date, enabled statuses, and whether any fresh allowance appears. Do not change the system clock. A fresh daily budget without approval fails the one-shot grant gate even if ordinary exhaustion succeeds.

## 7. Password and standard-account checks

Do this only after the unlocked tests. Use a disposable password and confirm recovery through the normal CT UI before locking a disposable block. Keep credentials out of shell history, tracing, screenshots, transcripts, and git. Do not paste a literal password into a command. A locally prompted shell variable avoids a literal history entry but still becomes a CLI process argument; it is not a security solution.

Test advertised `-start NAME -password VALUE` and `-stop NAME -password VALUE` locally with secret capture disabled. Compare no-password and wrong-password stop, correct-password stop, and `-as-is` with an existing configured lock. Record only success/failure and redacted errors. Check whether enabling/editing/status restrictions require additional authorization. Never change global password settings just to make a command work.

As the intended standard account, establish whether another same-account process can inspect arguments, read controller configuration, or invoke the CLI directly. Observe only your own disposable invocation; do not capture unrelated process arguments. Record ability to restart a preset without the secret and whether this refills allowance. If inspection cannot be performed without revealing a secret, record it as unresolved rather than saving a process dump.

## 8. Cleanup and evidence

Using the verified method, stop and remove only the explicitly named disposable blocks in CT's UI. If locked, use normal documented unlock; never bypass protection. Confirm unrelated blocks/settings still match the local baseline. Keep raw exports and experimental scripts local and ignored. Share redacted observations in the results table, including failures. No test result is a pass merely because a command returned zero.
