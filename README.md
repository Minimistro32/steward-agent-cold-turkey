# Cold Turkey feasibility: crash-safe usage grants

Investigation date: 2026-11-03. Status: **static investigation complete enough to begin Mac checks; live feasibility unverified**. No production agent, generator, server, or frontend changes are included.

## Decision

The midnight baseline design is supported by user verification of overlap precedence, automatic restoration including sleep/logout, intended preset reuse, and safe renewal ordering. Each new grant must explicitly cycle the selected allowance preset off/on under an enabled baseline. See the authoritative [updated design](docs/baseline-autostart.md).

Unused minutes may be forfeited at the daily cutoff. A proposed 03:45 cutoff remains unverified: baseline restoration must stay aligned with allowance renewal. Import generation, exact CLI behavior, independent exhaustion, and credential limitations remain open. The static matrix below records the initial investigation; user-confirmed results supersede its pending overlap/midnight entries.

The next step is the [guided Mac procedure](docs/mac-test-procedure.md). Record evidence in [results](docs/results.md). The [CLI reference](docs/cold-turkey-cli.md) describes embedded help, not commands executed on a Mac.

## Evidence and capability matrix

Evidence levels: **embedded** means readable help or strings in the supplied executable; **documented** means vendor documentation; **observed** requires execution on the target Mac. Static absence is not proof that an undocumented capability cannot exist.

| Capability | Evidence | Conclusion / remaining check |
| --- | --- | --- |
| CLI start, stop, status, list | Embedded help | Leading dash required by advertised syntax; runtime pending |
| Create website block and add targets | Embedded `-add-block`, `-add ... -web` | No advertised allowance setter; creation alone does not provision presets |
| Preserve configured settings | Embedded `-start ... -as-is` | Must test allowance, lock, and account scope preservation |
| Set allowance via CLI | Not found in embedded help | Unverified; do not invent an option |
| Unattended import/export | No advertised CLI option; import/export UI strings present | No verified unattended interface |
| Manual full-block import/export | Documented UI feature | Actual file format and merge/replacement behavior unknown |
| Generated presets | No real export supplied | Generator intentionally withheld until safe roundtrip validation |
| Enabled versus disabled status | Embedded `-status` help | Does not promise active enforcement, remaining usage, or exhaustion |
| Password start/stop | Embedded help and lock errors | Pro and settings restrictions; standard-account behavior pending |
| CLI alongside running CT | Not executed | Test with normal Mac UI/background components running |
| Usage definition | Documented | Foreground activity by default, configurable statistics behavior, mouse activity window; confirm installation behavior |
| Independent exhaustion | Intended CT allowance behavior | Must observe with orchestration absent |
| Repeat grants / overlapping presets | No runtime evidence | Reset, accumulation, and precedence unknown |
| Midnight renewal | Documented daily continuous allowance period | Likely conflicts with one-shot grants; live gate remains open |
| Sleep and logout/login | No runtime evidence | Usage persistence and renewal pending |

The vendor describes foreground counting by default, a three-minute mouse-activity window, configurable statistics behavior, and midnight-based continuous allowances. Its UI supports full-block import/export; website-list text export is a different feature. These facts are documentation evidence, not observations of this installation. Source: [Cold Turkey user guide](https://getcoldturkey.com/support/user-guide/), accessed 2026-11-03.

## Binary inspection

Input: attached `Cold Turkey Blocker` executable, not copied into this repository.
SHA-256: `38be42c0ce7901fe733683f328e9364bfde03cc0b2bada3ba8968d8e3759c972`.
Mach-O universal binary: x86_64 slice offset `0x4000`, length `1986272`; arm64 slice offset `0x1ec000`, length `1805072`. Linux cannot execute this macOS application. Installed Mac version and binary equivalence remain unknown.

Read-only inspection parsed Mach-O headers/symbol tables and printable strings. Relevant x86_64 absolute file offsets:

| Offset | Evidence |
| --- | --- |
| `0x19ed60` | CLI help introduction, advertising `-help` |
| `0x19fef0` | Error for trying to stop a locked block via CLI |
| `0x199470` | Full-block export UI label |
| `0x19a4b0` | `blocksToImportString` |
| `0x1a8065` | `blockAllowance` |
| `0x1a88a6` | `allowances` |
| `0x1ab920` | `application:openFile:` selector |
| `0x1ac84a` | `setAllowedFileTypes:` selector |

These selectors do not establish that opening a file imports it, or that import is unattended. Embedded default block data is not a validated export schema. No command-handler control-flow proof or complete reverse engineering is claimed. Do not use internal database paths or edit CT live files.

To reproduce the narrow help inspection locally (binary stays outside git):

```sh
shasum -a 256 '/path/to/Cold Turkey Blocker'
file '/path/to/Cold Turkey Blocker'
strings '/path/to/Cold Turkey Blocker' | rg -A 100 'Print available command line arguments'
strings '/path/to/Cold Turkey Blocker' | rg 'blocksToImportString|Export all blocks|blockAllowance|application:openFile:'
```

## Design gates and alternatives

Evidence may include explicit user verification, distinguished from assistant-run tests. The updated design records confirmations. Gates:

1. Starting the replacement before stopping the previous block never creates an unprotected state, including interruption at either step.
2. CT exhausts usage and blocks with the agent absent.
3. Restarting/reusing/alternating presets and crossing midnight do not create unapproved allowances.
4. Login-only credential exposure and invocation rights are understood and accepted.

If midnight renewal behaves as documented, a permanently enabled daily preset is unsuitable for a one-shot grant. Discuss a verified CT-native one-shot mechanism (if one exists), different grant semantics, or another enforcement integration. Scheduled allowances are not an established solution: schedule endings and subsequent recurrence need separate safety proof. Do not silently introduce a privileged helper or rely on a later agent callback.

Login-only operation puts the controller and managed user in the same account boundary. A centrally stored password must still reach the local invocation if the CLI requires it; passwords in process arguments may be observable. A keychain or private file should not be assumed to prevent the same account from using an authorized credential. Exact process visibility, keychain access behavior, and CT authorization remain live questions. Never record passwords in command transcripts, repository files, or shared exports.

A subsequent milestone must define usage grants, stable grant IDs/idempotency, remaining usage, multiple grants, renewal boundaries, reporting, and accounting. Steward's existing `AllowedUntil` is an absolute timestamp and is not equivalent to usage minutes. No protocol changes are made here.

Experimental scripts and raw evidence belong only under ignored `/tests/cold-turkey/` or outside the repository. Do not commit the commercial executable, credentials, or personal exports.

## Development setup

This repository will consume `Steward.Messaging` as an exact-version NuGet dependency, not a cross-repository project reference. See the [package build and restore instructions](https://github.com/Minimistro32/steward/blob/main/src/Steward.Messaging/README.md) in the sibling Steward checkout. The initial local version is `0.1.0-alpha.1`; it has not been published to a registry. No production agent project is scaffolded yet.

Keep experimental tests in ignored `/tests/` and private exports, credentials, and commercial binaries in ignored `/local/` (or outside this repository). Never commit these inputs. The current messaging contract has absolute expiry only; usage-grant protocol work remains a separate milestone.
