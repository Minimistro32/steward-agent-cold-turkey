# Candidate: CT-native midnight baseline restoration

User proposal received 2026-11-03. Evidence is a pasted export of two sample blocks, not an observed import or runtime test. Naming convention: `§teward-[resource]-[allowance time]`; the supplied baseline uses `§teward-test` and the one-minute preset uses `§teward-test-1`. Baseline naming should be made explicit before a generator is implemented.

The sample is an object keyed by block name. Both records use continuous mode, the same website target, all-user scope, no password lock, and string-valued enabled flags. The baseline has `break: none` and `autostart: window,0,0,0123456`; the preset has `break: allowance,1,m,day,0,0,0` and `autostart: none`. The user identifies the baseline configuration as midnight autostart. This is sample evidence, not a complete grammar for those encoded strings. The message contains escaped asterisks in the file exception; use an original export file to validate literal serialization rather than copying the rendered message as JSON.

Intended behavior: baseline normally blocks the resource. A grant selects the largest preset allowance no greater than the approved usage. CT independently exhausts that allowance. At midnight CT re-enables the baseline, preventing use of a renewed daily allowance even if the agent is absent.

## Accepted transition

User confirmation on 2026-11-03 establishes that the baseline overrides allowance blocks; midnight autostart restores a manually disabled baseline with the agent absent, including sleep/logout; preset reuse behaves as intended; and midnight renewal does not precede baseline enforcement. These are user-verified results, not tests executed by this assistant. A fresh allowance requires an explicit off/on cycle, even when the selected preset was the most recently used one.

For each new, distinct grant:

1. Resolve the exact resource mapping and select the largest preset no greater than the approved usage. If none fits, leave protection intact and report failure.
2. Enable and verify the no-allowance baseline, preserving its configured autostart. This is required even if a previous allowance is still enabled.
3. Disable all allowance presets for this resource, including the selected one, and verify the selected preset is off.
4. Enable the selected preset with its configured settings and verify it is enabled. Never optimize away its off/on cycle.
5. Disable the baseline last. On any earlier failure, retain the baseline and report failure.

This order protects the deliberate allowance reset. Interruptions before baseline removal leave blocking in place; after removal, the selected CT preset supplies enforcement. CLI observability and password handling still require validation before automating the verification steps. Use exact configured block identities, not broad prefix matching.

Serialize transitions per resource. A duplicate delivery of the same grant must not perform another off/on cycle: that would create additional allowance. Durable grant identity and interrupted-transition reconciliation belong to the later implementation milestone; do not blindly replay an uncertain reset after a restart.

## Daily cutoff

The user accepts forfeiture of unused minutes at the daily cutoff. Midnight is the confirmed configuration. A later cutoff such as 03:45 is a preference to explore, not a verified configuration or final selection.

Baseline restoration must coincide with or precede CT allowance renewal. Moving only the baseline autostart to 03:45 while allowance renews at midnight would create a window for unauthorized fresh usage. A later cutoff therefore requires a supported, validated matching allowance-renewal boundary, or retaining the midnight protection. Do not infer encoded time fields from the sample.

## Remaining work

- Validate disposable import roundtrip and preservation of unrelated blocks before generating presets.
- Establish exact CLI behavior, state observability, and preservation of autostart/settings through the off/on sequence.
- Validate independent usage exhaustion, detailed usage counting, and password/standard-account credential limitations not covered by the four user confirmations.
- If choosing a non-midnight cutoff, validate aligned renewal/restoration, including sleep/logout and transitions near the boundary.

The midnight baseline approach is accepted on the user's evidence. Full implementation remains gated by the remaining checks above. Existing Steward absolute-expiry messages still require a later usage-aware protocol/accounting design.
