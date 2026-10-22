# Free, Premium and Owner editions

DCS World is the only simulator available in Free Edition. Elite Dangerous and every other simulator integration require an authentic signed **Premium** or **Owner** entitlement. A reviewed Thrustmaster controller, keyboard-only profile, empty device list, imported configuration or remembered recent game never grants Free access to a reserved simulator. Future simulator identifiers default to requiring Premium; DCS uses the exact `dcs` backend ID.

| Capability | Free | Premium | Owner |
| --- | --- | --- | --- |
| DCS authoring/save/build/export/deployment | Reviewed Free hardware | All supported hardware | All supported hardware |
| Elite Dangerous and other simulator integrations | Unavailable | Available when implemented | Available when implemented |
| Physical input testing and identification | Unavailable | Available | Available |
| Appearance customization | Fixed Free appearance | Available | Available |
| Both isolated, simulated DCS training lessons | Available | Available | Available |
| Signed license required | No | Premium claim | Owner claim |

Premium and Owner are distinct authenticated identities with the same current feature access. Owner is Scott's private application-owner entitlement; it is not the credential distributed to Premium customers. Premium availability and pricing remain **TBA**. This contract adds offline signed Premium acceptance and a private issuer option, not checkout, a subscription, automated customer activation, expiry, machine binding or revocation. The commercial roadmap remains #252. Previously published release behavior is unchanged until a new release is published.

The existing Free hardware allowlist and appearance/input restrictions are retained. This change does not expand Free to every DCS controller. Ownership, commercial terms and distribution review remain release prerequisites; repository visibility and MIT notices are unchanged.

## Free release

Free provides the reviewed Thrustmaster catalog: Cougar MFD, TPR, Warthog throttle/grip on supported Thrustmaster bases, AVA/Viper and Viper TQS/Mission Pack. This is an explicit catalog-ID allowlist, not a promise to support every Thrustmaster device. MOZA base/grip aliases, VKB, WINCTRL, Logitech, other brands and unknown/generic layouts remain reserved. The legacy `grip-f18c` entry uses mixed-base evidence and is excluded pending a separately reviewed Thrustmaster-base definition. Connected hardware is checked using native HID vendor evidence, never its editable display name. Offline manual units still require a reviewed catalog template.

The fixed Flight Patch/Navy-Amber preset uses Scott's supplied Tomcat photograph at 20% requested image opacity, 10% theme blending, 35% panel opacity and 65% control opacity. Contrast protection may soften it further; Windows high contrast suppresses the photograph and decorative overrides. The photo is embedded and works without the original file. A second bundled background can be added through a future reviewed preset change; Free users cannot import/change the image or preset.

Theme selection, customization and reset are disabled and direct appearance-editor construction is rejected. Previously saved appearance files are retained untouched for Owner. Live input testing is blocked both at the start handler and native acquisition boundary. On current main (unreleased after v1.1.1.0), both interactive training lessons are free and demonstrate Premium capabilities with a fictional joystick and simulated input in an isolated temporary session. The banner and tester checkpoint explain that ordinary physical input testing/additional hardware/customization remain licensed. Training never changes the active account edition or acquires real controllers. The published v1.1.1.0 training gate remains unchanged until a new release is published.

Normal authoring, save, build, export and deployment remain available for supported workspaces. Unsupported/mixed workspaces are retained and displayed read-only; authoring/output operations reject reserved profiles. Imported custom drawings do not independently unlock reserved hardware: a compatible drawing on an allowed catalog identity retains that hardware’s edition rules. Hardware selectors filter reserved templates, and connected vendor checks reject incompatible physical units. Shared-default output resolves all profiles against the allowed catalog. Backup/recovery and clearing an existing deployment remain available. Free Saved Games imports start with an empty UI Layer baseline, avoiding inherited personal/non-Thrustmaster profiles.

## Signed Premium and Owner entitlements

Scott's owner entitlement unlocks all application features, hardware and appearance controls offline, without expiry, activation limits, checkout or a recurring service. It is a signed file, not a GUID password or a universal string. The application ships only the RSA public verification key. Claims require schema 1, product `HEIMDALL` (or the exact legacy `DCS-IPIA` claim), exact edition `Premium` or `Owner`, a nonempty issuance GUID and an owner name. Verification uses RSA-PSS/SHA256 over the exact UTF-8 payload. Inputs are bounded and reject malformed, unknown, duplicate, foreign-key and tampered data.

In **Settings → Install license…**, choose the privately delivered signed JSON license, then restart. The accepted file is stored under `%LOCALAPPDATA%\VyperIndustries\HEIMDALL-Licensing\owner-license.json`, outside profile backup/restore roots. It applies to all profiles for that Windows account. Invalid imports do not replace an accepted license. Missing/invalid files fall back to Free, retaining module and appearance data. Restarting with either valid paid entitlement restores saved customization. The historical `owner-license.json` filename and claims types are retained for compatibility; the signed edition determines access and the displayed identity.

Signed customer/owner licenses and the private signing key must never be added to GitHub, support reports, CI variables/artifacts, installers or ordinary profile backups. Keep the recovery package separately backed up. The recovery key can issue another owner license using PowerShell 7 and `scripts/Issue-OwnerLicense.ps1 -SigningKeyPath <private-key.pem> -OutputPath <new-owner-license.json>`. The script verifies that the key matches the pinned application key before writing. There is no production environment-variable, debug-build or release-smoke owner bypass. Automated tests use ephemeral keys that do not authenticate against the shipped application.

A customer Premium license can be issued privately with PowerShell 7:

```powershell
pwsh -File .\scripts\Issue-OwnerLicense.ps1 `
  -SigningKeyPath <private-key.pem> `
  -OutputPath <premium-license.json> `
  -OwnerName "Customer name" -Edition Premium
```

Omitting `-Edition` retains the existing Owner issuance behavior. Only `Owner` and `Premium` are accepted, the key must match the pinned public key, and an existing output file is never overwritten. Editing the edition in an existing signed payload invalidates the signature. Both entitlements are offline, portable and non-expiring; do not infer customer seat enforcement from signature validation.

## Local and VM edition testing

Close every HEIMDALL instance and privately preserve your working license. Rename any `owner-license.json` to `owner-license.json.disabled` in **both** `%LOCALAPPDATA%\VyperIndustries\HEIMDALL-Licensing` and the legacy `%LOCALAPPDATA%\VyperIndustries\DCS-IPIA-Licensing` directory. Startup resolves the legacy file when the newer one is absent. Restart and verify Settings reports Free. Restore the original filename, or install a preserved signed JSON through Settings, then restart to restore Premium or Owner. No uninstall, profile reset or database deletion is needed.

Licensing is Windows-user-wide: a named test profile does not independently change editions. Use separate Windows accounts or clean VM checkpoints to retain independent Free, Premium and Owner environments. Install the exact candidate package. Test Free DCS authoring/output and restricted controls, blocked ED cards/recent-game entry/direct commands, paid ED reopen/edit/export/input, identity labels, invalid-import preservation, and Free fallback/restoration without losing saved data. VM snapshots containing signed licenses must stay private; signing keys stay outside the VM. Named test profiles disable Fly Now/Clear deployment; full deployment acceptance uses a normal installation in the disposable VM. Hardware/in-game acceptance still requires verified native device access.

## Code and package separation

This PR corrects runtime entitlements in the existing repository; the unified package still contains ED implementation code. A new repository is **not required** for this contract correction. Keeping future Premium implementation private requires a separate private module repository/build boundary, while reusing the public Free/shared foundation rather than duplicating forks. A later Free distribution must omit paid modules/assets and verify that omission in CI. Current or historical public source is not made private by this change.

The existing MIT/public-source distribution means these client-side controls are not a promise of tamper-proof DRM. Customer commerce, payments/refunds, supported upgrades, managed activation/recovery and service operations remain separate work in #252.

## Validation

Unit tests exercise exact catalog/vendor rules, Premium/Owner identities and feature access, simulator boundaries, invalid claims, wrong keys, signature tampering, duplicate/oversized documents, atomic activation and recovery. Windows smoke starts Free, creates/saves an actual Thrustmaster module, checks fixed photo/theme and preserved preferences, rejects a reserved hardware edit/direct input start, then verifies signed Premium and Owner appearance restoration. The ED authoring/input regression suite runs as Premium, rejects Free cards and direct ED construction without touching saved data/native input, and reopens the retained profile as Owner. Exact published payload checks continue to run as Free. Main-window/settings/workspace captures include the supplied photo; production owner credentials are never part of those artifacts.

## Module snapshot import

Mixed-hardware snapshots offer **Remove unsupported devices and import** or **Cancel import** before database insertion. The review lists excluded device instances. Confirming removes unsupported device memberships, their assignments/modifiers/device kneeboards, and supported-device assignments that need those removed modifiers, independently in Aircraft and UI Layer. It preserves other supported assignments/tuning/labels, catalogs and the original snapshot file. Nothing is written on Cancel, Escape or close; when no supported device profile would remain, conversion cannot be accepted. The filtered snapshot must build and verify successfully before insert-only restore. Already compatible snapshots and Owner snapshots retain the original lossless path. Existing saved mixed-hardware modules keep their existing edition restrictions; this change does not silently prune previously saved data.

The existing Free keyboard-profile exception is retained: a compatible keyboard profile can survive removal even when all controller profiles are unsupported.

### Sol-R grip on AVA

The four bundled `tm-sol-r-ava-{r,l}-{buttons,pov}` hardware identities are
available in Free Edition. See [Sol-R on AVA](../sol-r-ava.md) for setup and
shared layout selection. Native Thrustmaster verification still applies; this
does not unlock other bases, unknown layouts, appearance customization or
live input testing.
