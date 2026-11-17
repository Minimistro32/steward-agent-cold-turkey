# Server-managed agent secrets and locked agents

Status: agreed design direction, not an implemented feature or final protocol specification. Updated 2026-11-16. Revisit after alpha.8 Mac signing/install/background behavior is verified. This note supersedes the earlier per-resource credential proposal; its filename is retained to preserve links. No passwords or new locks are introduced by this document.

## Goal and scope

Provide friction against impulsive manual bypass while keeping recovery possible. Steward should support an optional configurable secret per agent and an independently administered locked-agent policy. The abstraction should work for a Cold Turkey password, a Pi-hole API key, or another agent's credential without adding product-specific server behavior.

For Cold Turkey, use one stable configurable password for all Steward-managed blocks belonging to that agent, including every resource baseline and allowance preset. Unrelated Cold Turkey blocks are outside its scope. Separate agents can have separate secrets. Do not rotate on every grant or activation: that adds partial-failure and recovery risk for little benefit to this use case.

This is friction, not protection against a determined local administrator. The agent must eventually supply the credential to its enforcement tool; memory, command arguments or local tool storage may expose it. Ordinary configuration files, menus, imports and logs should not disclose the secret. Server custody alone cannot make local extraction impossible.

## Independent administration

A locked agent must always have at least one eligible administrator. An eligible administrator is an admin who is not a user of any device controlled by that agent. Being an admin does not grant an exception for one's own device.

Only an eligible administrator may change or reveal the locked agent's secret, change its controlled-device associations, or remove its locked status. Ordinary allowance requests remain separate from secret administration. Device use is based on Steward's recorded user/device associations; the system cannot determine who physically uses a laptop.

Enforce this as a server-side invariant across every operation that can affect it:

- Enabling protection requires an eligible administrator and independent recovery confirmation.
- Deleting or demoting an administrator must not remove the last eligible administrator for a locked agent.
- Changing user/device assignments or agent/device associations must not leave a locked agent without an eligible administrator.
- An administrator cannot assign a locked agent's device to themselves through an administrative action that requires independence.
- A device user cannot remove their own association to manufacture eligibility, then edit the secret or protection policy.
- Deleting, replacing, or re-registering an agent must not bypass protection or silently discard its recovery information.

These checks must apply to APIs and background registration processing, not only UI controls. Validate authorization and the resulting state in one transaction or equivalent concurrency-safe operation. Reject changes with a useful explanation and a route to appoint another eligible admin first. An independent administrator may legitimately reassign a device; the exact transfer workflow still needs design.

These rules prevent lockout through ordinary Steward administration. They cannot guarantee recovery after a server wipe, loss of all administrator accounts, or loss of the external password copy.

## Generic capability and ownership

Keep credential support, credential configuration, administrative protection and actual local application distinct. Suggested names are provisional:

| Concept | Owner | Meaning |
| --- | --- | --- |
| `secretSupported` | Agent advertises | This agent accepts a server-managed secret. |
| `secretLabel` | Agent advertises | Human-readable UI label, such as “Block password” or “API key”. |
| `secretConfigured` | Server derives | A usable secret has been saved centrally; not proof it works locally. |
| `locked` | Server controls | Independent-admin restrictions apply to this agent. |
| Application status/version | Agent reports; server tracks | Whether the intended secret was applied or validated, or the result is pending/uncertain. |

An agent's registration must not be able to clear server-owned protection by sending `locked: false`, omitting a field, changing its advertised capabilities or reconnecting. Registration describes capabilities; privileged server actions establish policy. Existing agents without secret support continue to work unchanged. Exact negotiation and backward compatibility remain to be specified.

A configured secret does not necessarily imply a locked agent: an API key can be needed for ordinary integration without this administrative policy. Likewise, server policy must not claim local blocks are password-protected solely because `locked` is true. The UI should show application failures or uncertainty separately.

The first version has one configurable secret per agent. Some agents may eventually need several credentials; defer a multi-secret schema until there is a concrete need. The agent owns product-specific validation and local behavior. For example, CT may apply a chosen password while another integration validates an externally issued API key. The generic server must not assume every secret can be generated or rotated by the agent.

## Secret custody and delivery

Store recoverable secrets centrally with encryption at rest and an explicit key-recovery strategy; hashes alone cannot provide credentials to agents. Deliver secrets only to the authenticated, authorized agent that needs them. Agent IDs are not authentication.

Do not put secret values in registration, normal resource responses, retained MQTT advertisements, or diagnostic logs. Authenticated agent identity, broker ACLs and encrypted transport need design before secret distribution; the current anonymous plaintext LAN connection does not provide confidential delivery.

Track desired and confirmed secret versions and pending/uncertain transitions durably. A sent command or MQTT acknowledgement is not proof the local tool accepted the secret. The current response handling logs outcomes; durable application tracking is additional work. On restart, reconcile with the server rather than generating a replacement secret because process memory was lost.

Whether delivery occurs at connection, on demand, or during scoped operations remains open. Prefer no plaintext local persistence. An agent restarting while the server is unavailable may consequently be unable to service new grants; document and test that tradeoff. Preserve existing enforcement and avoid transitions that cannot finish or recover safely.

## External recovery and password changes

Before enabling CT password protection, instruct the eligible administrator to save an external copy of the configurable agent password, and require confirmation. A separate password manager or other independently accessible secure record is sufficient as the initial direction; a custom encrypted recovery-export feature is not required for the first version.

The record should identify the agent, its device(s), covered managed blocks, and how to manually unlock them without Steward or the agent. It must remain accessible after loss of the server and its encryption keys. A copy stored only on that server is insufficient. Confirmation records the administrator's acknowledgement; it cannot prove the copy remains available forever.

Demonstrate manual recovery on disposable blocks before enabling real protection. Never promise that uninstalling CT, uninstalling the agent, or waiting will remove password locks. Avoid device-wide locks in the first pilot.

When intentionally changing a CT password:

1. Authorize an eligible administrator and preserve the existing password/version.
2. Save the new password centrally and require confirmation that both old and new values are available externally before applying it.
3. Apply the change across all managed blocks and collect explicit outcomes.
4. Keep both versions while any block may still use the old one, including partial failures and lost acknowledgements.
5. Retire the old version only once reconciliation confirms it is no longer needed for recovery.

Other agents may validate or replace secrets differently; these mechanics belong in the agent integration, with generic server tracking of outcomes. Exact retirement and retention policies remain open. A resource rename, deletion, agent replacement or decommission must not silently orphan protected targets or destroy needed secrets.

## Maintenance

Prefer independently approved maintenance over exposing the password for routine work. Any reveal or change for a locked agent requires an eligible administrator and an audit record that excludes the value itself. How reveal, maintenance approval and any subsequent rotation work is still to be tuned.

A time-limited server approval does not itself relock Cold Turkey after expiry. Local restoration and failure recovery must be designed and verified before offering that promise. Removing server policy is also distinct from successfully removing local password protection; decommissioning needs confirmation from the agent or a documented manual recovery path.

## Cold Turkey feasibility gates

The observed CLI documents password-based start/stop operations, but does not prove they safely compose with the existing allowance implementation. Consult `docs/cold-turkey-cli.md` and verify on disposable blocks:

- Does password-based starting preserve usage allowances, refill times and baseline autostart? Do not assume separate `-as-is` and `-password` forms can be combined.
- Does stopping remove password protection, and does scheduled/autostart reactivation restore it?
- Can disabled presets remain protected against editing?
- Is a separate global settings password needed, and would that affect unrelated blocks?
- Can application of a password be verified beyond enabled/disabled status?
- What happens after a crash, partial rotation, wrong password, or offline baseline restoration?
- Do CT exports expose passwords or derived values?
- Can an administrator manually recover without either server or agent?

The answers may constrain implementation without changing the generic independent-admin policy.

## Details still to tune

- Exact field names, protocol versioning, authenticated secret delivery, and application acknowledgements.
- Whether eligible admins are all qualifying admins or an explicit authorized subset; the invariant applies to whichever model is chosen.
- Legitimate device ownership transfers, multiple device users, and the safeguards against self-removal of associations.
- Admin onboarding, account recovery and agent identity replacement without bypassing existing protection.
- Secret-entry UI, optional generation, CT-compatible length/characters and validation. A long generated alphanumeric password may be a useful default, not a fixed generic requirement for API keys.
- Recovery confirmation wording, manual recovery instructions, reveal policy, version retention and decommissioning.
- How newly created blocks receive the current secret safely, including acknowledgement and recovery mapping.
- Exact offline behavior and whether any local credential caching is justified.

## Later sequence

1. Verify alpha.8 Mac packaging and background behavior and resolve issues.
2. Validate CT password semantics and independent manual recovery.
3. Specify the minimal generic agent-secret and locked-agent extension, including the authorization invariant and transfer workflows.
4. Implement server custody, authorized delivery and durable transition tracking, then agent integration.
5. Test admin deletion/demotion, device reassignment, self-association removal, concurrent updates, registration downgrade attempts, offline restarts, partial password changes and simulated server-loss recovery.
6. Pilot one low-impact managed resource before expanding to all managed blocks.
