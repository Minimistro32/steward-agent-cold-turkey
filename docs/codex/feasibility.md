# Cold Turkey feasibility evidence

The current workflow uses the working `Sample.ctbbl` supplied by the user, shared between Windows and macOS. Setup directly collects one resource's targets and allowance choices, writes the import file, and waits for manual import/autostart confirmation. Template selection and roundtrip gates are superseded. See [setup](setup.md) and [verification](verification.md).

User-confirmed baseline precedence, restoration while the agent is absent, off/on allowance reset and renewal ordering are accepted. The profile is now baseline at 03:30 every day, allowance refill at 03:45. Password locking is deferred; future passwords are intended to come from Steward.

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

## Evidence boundaries

The sample validates the exported field structure; it does not supply live CLI stdout for either OS. Automatic status parsing accepts only whole boolean/enabled tokens, with configurable explicit overrides for other versions. Unknown output fails safely.

Local tests use a fake CT executable and MQTT peer. Actual macOS login/CLI execution and Windows runtime behavior remain installation checks. No commercial binary or personal export is tracked. The current adapter intentionally converts remaining server time plus 30 seconds into a CT usage preset; later usage-aware server accounting remains separate work.
