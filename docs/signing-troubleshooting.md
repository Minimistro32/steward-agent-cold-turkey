# Developer ID signing troubleshooting

## Successful alpha.8 signing on 2026-11-17

The user's Mac found Developer ID Application: Tyson Freeze (RZ775S6QM6), but Sign Steward.command failed at bundle signing with “A timestamp was expected but was not found.” Retrying on a phone hotspot produced the same error.

A temporary copy of /usr/bin/true was then successfully signed with the certificate name, hardened runtime and --timestamp. Strict verification passed, with Timestamp=Nov 17, 2026 at 12:13:55 PM. This demonstrated that the identity/private key and timestamp access worked at that moment independently of the app.

The following manual app signing command subsequently succeeded. For future releases, change the cd path to the newly extracted release. Stop that agent and close its menu first. The identity name is public certificate metadata, not a secret; other publishers must substitute their own valid Developer ID Application identity.

```bash
(
  set -e
  cd "$HOME/Downloads/steward-cold-turkey-0.1.0-alpha.8-osx-arm64"

  codesign --force --verbose=4 \
    --sign "Developer ID Application: Tyson Freeze (RZ775S6QM6)" \
    --identifier org.steward.coldturkey.agent \
    --options runtime --timestamp \
    --entitlements "$PWD/entitlements.plist" \
    "Steward Cold Turkey.app"

  codesign --verify --deep --strict --verbose=4 \
    "Steward Cold Turkey.app"

  codesign --display --verbose=4 "Steward Cold Turkey.app"
)
```

Observed app results:

- Signing succeeded; strict/deep verification reported valid on disk and satisfies its Designated Requirement.
- Identifier=org.steward.coldturkey.agent.
- Authority=Developer ID Application: Tyson Freeze (RZ775S6QM6), chaining through Developer ID Certification Authority to Apple Root CA.
- TeamIdentifier=RZ775S6QM6.
- Timestamp=Nov 17, 2026 at 12:14:35 PM.
- Hardened-runtime flag present; sealed resources and bound Info.plist present.

This command is sufficient for alpha.8's single native executable bundle. If future packages add nested native components, sign those inside out first as the packaging script does; do not assume this outer-bundle command replaces that step.

## What is and is not established

The script selected the identity by fingerprint; the successful command selected it by name and added --verbose=4. Both selection forms are valid. The successful invocation also happened later. We have not isolated a cause: neither a fingerprint-selection defect nor a transient Apple service failure has been proved. Switching networks alone did not fix the observed error. Do not document the manual invocation as a proven root-cause fix.

For a future recurrence, preserve verbose output. Use the successful invocation above as a diagnostic/recovery path and verify the expected authority, team, identifier and secure timestamp before creating the DMG. Do not remove --timestamp or fall back to ad-hoc signing to suppress the error. No certificate regeneration was required in this incident.

A potential future script improvement is verbose signing output and bounded retries restricted to this timestamp failure, followed by full verification. This improvement has not been implemented. Keep other signing errors fatal.

Apple has documented historical service-side instances of the same error, which is context rather than a diagnosis of this incident: https://developer.apple.com/forums/thread/654794

## Next validation

The user can now run Create DMG.command using the successfully signed app. Actual DMG creation, installed Finder launch, hardened .NET runtime execution, config migration and background MQTT behavior remain unconfirmed for alpha.8. Developer ID signing is confirmed on the user's Mac; notarization is still deferred. The Linux preparation archive is unchanged and is not itself Developer ID-signed.

## Follow-up: standardize future releases

User requested on 2026-11-17 that we standardize this procedure for future updates, or first discuss industry best practice. This is planned discussion/work, not authorization to enable notarization or introduce a new distribution service now.

Before the next release, review current Apple guidance and common macOS release practices, then agree on a repeatable process covering:

- Where release builds and Developer ID signing run, with safe private-key custody and consistent signing identity across updates.
- Automated inside-out signing, secure timestamps, verification, useful diagnostics and bounded handling of transient failures.
- Whether to add notarization and stapling, including their prerequisites and operational effort; notarization remains deferred until agreed.
- DMG creation and verification, versioning, checksums and retention of release evidence.
- A clean-Mac installation check and an upgrade check that preserves config, login startup, permissions and background connectivity.
- The user-facing update process: manual replacement initially versus a future updater, with recovery/rollback considerations.

Document the chosen procedure and automate appropriate steps so future versions do not rely on remembered Terminal commands. Distinguish Apple requirements from optional conveniences and our own testing choices. Do not label the current ad hoc troubleshooting sequence as industry best practice.
