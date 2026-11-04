# Feasibility results

2026-11-03: only Linux-hosted static inspection and vendor-document review performed. No Mac commands, imports, allowance measurements, password tests, or midnight tests executed. User subsequently supplied a two-block JSON example with a midnight-autostart baseline and one-minute daily allowance. Schema sample available; import and behavior validation remain pending. No generator produced.

| ID | Check | Status | Evidence required |
| --- | --- | --- | --- |
| S1 | Binary identity and embedded CLI | Static evidence recorded | See README and CLI reference |
| S2 | Allowance setter / unattended import | Not established | Supported option or observed supported interface |
| M1 | Installed version / exact CLI / coexistence | Pending Mac | Version, redacted help/output, exit status |
| M2 | Disposable export schema and safe import | Pending export | Sanitized sample, merge behavior, unchanged unrelated blocks |
| M3 | Generated preset roundtrip | Blocked by M2 | Targets, allowance, lock, account scope preserved |
| M4 | Overlap and transition interruption | Pending Mac | Actual navigation and allowance at each step |
| M5 | Foreground, idle, background, closure | Pending Mac | Timestamped counter/navigation observations |
| M6 | Sleep and logout/login | Pending Mac | Counter and protection persistence |
| M7 | Same/alternate/repeated start | Pending Mac | Reset/refill and duplicate behavior |
| M8 | Exhaustion without controller | Pending Mac | Blocking after consumed usage |
| M9 | Exhausted and partial natural midnight | Pending Mac; documented concern | No unauthorized renewed allowance |
| M10 | Password and standard-account boundary | Pending Mac | Redacted outcomes and exposure limits |
| M11 | Enabled versus enforcing observability | Pending Mac | CLI/UI/navigation comparison across states |

For each observation record: test ID; CT/macOS/browser versions; timezone and timestamps; disposable block names; initial enabled states and remaining allowances; exact command with secrets omitted; exit status; redacted stdout/stderr; UI counter; fresh navigation result; final state; conclusion. Record unexpected results and cleanup status.

Decision: implementation not recommended yet. Daily renewal is a documented incompatibility risk, not a measured result. Discuss alternatives if confirmed; retain login-only deployment unless explicitly changed.

## User verification update — 2026-11-03

The user confirms baseline precedence over enabled allowances, midnight restoration with the agent absent including sleep/logout, correct preset reuse, and no transient allowance-renewal gap before baseline enforcement. Accept these findings without requiring the user to repeat them. M4 overlap precedence, M6 midnight restoration, M7 intentional preset reuse, and M9 renewal ordering are user-verified; interruption recovery, other usage persistence, and duplicate-delivery handling are not thereby tested. Selected presets must explicitly turn off and on for each distinct grant. Unused minutes may be forfeited at cutoff. Midnight is verified; a possible 03:45 cutoff needs aligned renewal validation.

This supersedes the initial daily-renewal blocker above for the midnight-baseline design. Import roundtrip, CLI observability, independent exhaustion, and password/account boundary checks remain open. See [updated transition](baseline-autostart.md).
