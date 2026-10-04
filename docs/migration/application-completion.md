# Application completion

This is the current application completion scope agreed on 2026-10-03. It supersedes historical requirements to duplicate desktop draw.io or regenerate consumer repository scripts inside IPIA.

| Requirement | Evidence / exit check |
| --- | --- |
| Create a module or start from Saved Games, a consumer repository or a portable snapshot | Existing creation/import tests, Windows smoke and user authoring acceptance; #111 is closed at the user's request. |
| Edit device instances, commands, assignments, modifiers, axis tuning and removals, device/summary pages and integration guidance | Merged authoring PRs; SQLite Save/Discard, device isolation and removal-evidence regressions. |
| Maintain shared UI defaults with isolated module overrides and revision adoption/rollback | Shared-workspace tests and accepted #83 Step 2. |
| Own shared assets and supported SVG export without a Common checkout at runtime | Pinned asset/license integrity checks and exact all-12 SVG comparison in PR #127. |
| Generate a validated OvGME/Complete package and rebuild/roll back from captured snapshots | [Huey release acceptance](huey-release-acceptance.md), plus module creation/snapshot/Complete verification tests. |
| Ship a tested Windows installer and portable application | CI release-candidate validates actual installed, portable and extracted executable startup, payload hashes and install/uninstall. |

Desktop draw.io remains the tool for creating and editing graphic sources. Legacy consumer repository creation, script/workflow regeneration, GitHub publishing/cutover and repository archival remain optional operational work. They do not block completion of this application scope. Existing repository history and releases remain preserved.

Completion is assessed against the final PR's green checks, measured release differences and tested application release candidate. A Huey reference establishes a real consumer acceptance case, not an assertion that every module was manually flown in DCS. Generic Saved Games/module tests cover other modules; newly discovered defects should be tracked as defects rather than silently expanding the completion checklist.

The parity report exposes generated page hashes/dimensions and captured package/documentation changes. Visually different output is not called byte-identical parity. User review of those differences and merging the final PR are the remaining review steps; an explicit failed acceptance check is a concrete blocker.

Current assessment: the two concrete output defects found in #128 are corrected. All released Huey aircraft/UI bindings must now match semantically; mapped labels must fit the authored callout geometry without losing variants. Snapshot rebuild/rollback remain required. Completion now depends on green final CI (including the actual Windows release candidate), review and merge of the correction. No additional implementation milestone is planned under this agreed scope. A real defect found in those checks remains a blocker; optional repository cutover work does not become one.
