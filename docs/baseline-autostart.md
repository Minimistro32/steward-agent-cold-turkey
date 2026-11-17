# Baseline and allowance design

Current setup creates one resource at a time using the user-supplied shared CT format. Its baseline is named `§teward-[resource name]`; new allowances are `§teward-[resource name]-[four-digit minutes]` (for example `-0005`). Every block has the same websites, applications and exceptions.

The baseline has no allowance and autostarts at **03:30 every day**. Presets refill at **03:45 every day**. After import the user must manually choose **Time of Day → 3:30 a.m. → every day** for the baseline, then confirm setup. The agent starts that baseline for the first time; presets remain disabled. No password locks are applied yet.

The user has verified baseline precedence over allowance blocks, restoration while the agent is absent including sleep/logout, intended allowance reset behavior, and renewal ordering without a gap. These are user-verified results, not assistant-run CT tests.

For each distinct, unexpired grant:

1. Choose the largest preset no greater than `AllowedUntil − current UTC time + 30 seconds`.
2. Enable and verify the baseline.
3. Disable every discovered allowance preset, including the selected preset, and verify each is disabled.
4. Enable and verify the selected preset.
5. Disable the baseline last.

The selected preset must always be cycled off/on to reactivate allowance. New grants replace unused previous allowance. Failures attempt to restore the baseline; uncertain failures are logged and not automatically retried.

Ordinary startup/shutdown leave CT unchanged. There is no durable grant journal. In-memory duplicate handling prevents repeat resets during one process lifetime; a redelivery after restart can reset allowance again. Only config.json persists agent identity/connection settings. Setup confirmation can be resumed by selecting the imported resource directly from CT; it has no session file.

Unused minutes may be forfeited when the baseline returns. The usage allowance can outlive Steward's absolute timestamp; this adapter deliberately applies the agreed conversion and transit tolerance. Future usage-aware accounting and server-managed passwords are separate work.
