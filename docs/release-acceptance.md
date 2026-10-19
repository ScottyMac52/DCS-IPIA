# Release acceptance record and template

This is the candidate evidence contract for [#188](https://github.com/ScottyMac52/DCS-IPIA/issues/188). [Distribution readiness](distribution-readiness.md) supplies the detailed scenarios and support privacy contract. [#245](https://github.com/ScottyMac52/DCS-IPIA/issues/245) indexes current outstanding work. Historical migration issues #83/#9/#111 are closed and do not replace this release acceptance.

## Recorded baseline: v1.1.1.0

This identifies the published candidate; it does not claim new hands-on acceptance or independent inspection of downloaded payloads. Reviewed on 2026-10-19 against GitHub source/release/workflow metadata.

| Field | Recorded value |
| --- | --- |
| Application/tag | [v1.1.1.0](https://github.com/ScottyMac52/DCS-IPIA/releases/tag/v1.1.1.0) |
| Source commit | `c74a061cd6a774ee0db3e45f6894865c41165a7e` |
| CI | [39200029794](https://github.com/ScottyMac52/DCS-IPIA/actions/runs/39200029794), completed successfully |
| Release workflow | [39200677440](https://github.com/ScottyMac52/DCS-IPIA/actions/runs/39200677440), completed successfully |
| Portable asset | `DCS-IPIA_v1.1.1.0_win-x64.zip` |
| Portable SHA-256 | `3fc66ba2f1746f2cf40eaa291c999ad4c8c5447d87de36783d6ac69b99aefd6e` |
| Installer asset | `setup.exe` |
| Installer SHA-256 | `f2faf0dded42481b64c9068e4407f5811d9041879515f7ca85fefb6852418add` |
| Other release evidence | `TEST-EVIDENCE.json`, `DISTRIBUTION-EVIDENCE.json`, `SHA256SUMS.txt`; asset digests observed in GitHub metadata, contents not independently approved here |
| Hands-on tester / Windows / DCS / controller firmware | Pending |
| Distribution reviewer and decision | Pending |

### Work ownership and observable acceptance

| Issue | Observable completion | Current evidence/result |
| --- | --- | --- |
| [#188 — acceptance contract](https://github.com/ScottyMac52/DCS-IPIA/issues/188) | Candidate identity/hashes/CI, reusable template, outcomes, blocker rules and current documentation are linked together. | Delivered by this documentation change; review/merge required. |
| [#189 — controllers/DCS](https://github.com/ScottyMac52/DCS-IPIA/issues/189) | Supported controls resolve intended commands; repeated units stay independent after reconnect; intended axes/filters/modifiers and aircraft/UI Layer work in DCS; save/reopen/snapshot round trip retains assignments. | Automated workflow support exists; current candidate hands-on matrix pending. Run reviewed Free hardware separately from Owner generic/custom coverage. |
| [#190 — upgrade/recovery](https://github.com/ScottyMac52/DCS-IPIA/issues/190) | Identified older-release data survives upgrade/reinstall; isolated restore retains expected sources/pins/assignments/output and previous originals; interrupted restore restarts consistently. | Automated current-candidate replacement/recovery exists; actual older-release/data observations pending. |
| [#191 — usability/diagnostics](https://github.com/ScottyMac52/DCS-IPIA/issues/191) | Required actions remain reachable by keyboard; focus/text are readable at recorded DPI/high contrast/window size; help matches behavior; representative failures produce actionable messages/logs and private support previews. | Theme/layout/training automation exists; complete current Windows matrix pending. Include [#264](https://github.com/ScottyMac52/DCS-IPIA/issues/264) TQS/VM/DPI checks. |
| [#192 — artwork](https://github.com/ScottyMac52/DCS-IPIA/issues/192) | Every shipped asset has source/redistribution evidence or a reviewed replacement/removal, including embedded raster images and newer branding/backgrounds. | Historical source audit exists; remaining asset decisions pending. Free feature restrictions do not prove reserved assets were removed from the payload. |
| [#193 — notices](https://github.com/ScottyMac52/DCS-IPIA/issues/193) | Exact portable/installed dependencies, native components and runtime versions have applicable full notices; reviewRequired entries have recorded dispositions. | Notice collection/hash checks exist; candidate completeness review pending. |
| [#194 — sign-off](https://github.com/ScottyMac52/DCS-IPIA/issues/194) | Named maintainer records candidate-specific results, resolved blockers, known limitations and distribution decision. | Pending. Published release and green CI are not sign-off. |

[#155](https://github.com/ScottyMac52/DCS-IPIA/issues/155) and [#183](https://github.com/ScottyMac52/DCS-IPIA/issues/183) remain open umbrellas at this audit. This documentation does not close or consolidate GitHub issues. Their requirements remain applicable until recorded completion or explicit scope transfer.

## Outcome and blocker rules

- **Pass:** record the expected and actual result, tester/date, exact candidate and attached evidence. An automated pass applies only to its tested scenario.
- **Fail:** record the actual deviation, reproducible steps, affected edition/platform and linked defect. Corruption/data loss, failed install/recovery, incorrect native mappings/deployment or inaccessible essential controls block the affected release scope until fixed and retested.
- **Pending:** a required check has no executed observation. It cannot be marked passed or silently treated as not applicable.
- **Not applicable:** a named reviewer records why the check is excluded from the intended release scope. For example, generic hardware is outside Free authoring but remains relevant if those assets/features ship in Owner or a shared payload.
- A nonblocking limitation needs an explicit owner decision, reason and user-facing note. A failed/pending rights or notice review remains unresolved until the responsible maintainer records its disposition. CI integrity checks do not supply that decision.
- Do not close acceptance umbrellas or claim candidate sign-off while required rows are pending/failed without an explicit scope decision. Publishing is a separate event.

## Copy for the next candidate

Record application/tag, full commit, CI/release run, installer/portable filenames and SHA-256, TEST-EVIDENCE/DISTRIBUTION-EVIDENCE/checksum identities, intended edition/distribution scope and reviewer/date. Record Windows build, DCS version, physical controller/base/grip model and firmware, actual logical client area/DPI/scaling, baseline app release for upgrades and backup/restore procedure.

| Scenario / issue | Expected result | Actual observation | Outcome | Tester / date / platform | Evidence / defect / disposition |
| --- | --- | --- | --- | --- | --- |
| Clean install/setup and supported Free authoring — #189 | Install without developer tools; explicit device adoption; assign/save/reopen/build/deploy intended controls. | Not executed | Pending | Pending | Pending |
| Owner generic/custom/mixed/repeated units and reconnect — #189 | Independent mappings/pins; actual button/hat/axis behavior matches preview and output. | Not executed | Pending | Pending | Pending |
| F-15E and another aircraft; aircraft/UI Layer — #189 | Intended axes, filters, chords and unused-controller cleanup work in DCS. | Not executed | Pending | Pending | Pending |
| Saved snapshot round trip — #189 | Intended assignments/labels/tuning survive verified insert-only restore; Free mixed import changes only reviewed unsupported content. | Not executed | Pending | Pending | Pending |
| Older-release upgrade and uninstall/reinstall — #190 | Saved modules/artwork/pins/preferences/output survive; edition and separately stored license behave as documented. | Not executed | Pending | Pending | Pending |
| Backup/restore and interrupted restart — #190 | Retained originals, restored state and deployment-review behavior match the recovery contract. | Not executed | Pending | Pending | Pending |
| Keyboard/contrast/DPI/layout/help/errors — #191/#264 | All essential actions reachable and readable; current help and diagnostics accurate. | Not executed | Pending | Pending | Pending |
| Shipped artwork and dependency/runtime notices — #192/#193 | Exact-payload evidence and required notices have recorded decisions. | Not reviewed | Pending | Pending | Pending |
| Final release decision — #194 | Required evidence complete; blockers resolved or explicitly scoped; known limitations recorded. | Not decided | Pending | Pending | Pending |

Keep raw private paths, GUIDs, logs, images, license files and snapshots out of public evidence unless separately reviewed. Prefer the scoped support report and redacted attachments. Named test profiles cannot deploy to DCS; use a separately backed-up, reviewed Production configuration for actual in-game checks. User-data backups do not cover DCS deployments or separately stored owner credentials.
