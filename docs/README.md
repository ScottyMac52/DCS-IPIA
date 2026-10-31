# HEIMDALL documentation

The current documented release baseline is **v1.1.1.0**. Read the [release notes](../release/RELEASE-NOTES.md) for that baseline and [GitHub Releases](https://github.com/ScottyMac52/DCS-IPIA/releases) for published downloads. `main` may contain newer unreleased changes. Guides describe implemented behavior unless explicitly marked historical, proposed or pending acceptance.

## Start and use the application

| Task | Guide |
| --- | --- |
| Understand Free, Owner and planned Premium access | [Edition availability and activation](design/free-edition.md) |
| Sol-R grip on AVA: right/left and POV/button layouts | [Bundled Sol-R hardware](sol-r-ava.md) |
| Set up controllers and a Saved Games destination | [Hardware setup](hardware-onboarding.md), [inventory](hardware-inventory.md), [settings](ux/settings.md) |
| Create or import a module | [New module](new-module-creation.md), [authoring and Saved Games import](module-authoring.md) |
| Navigate the eight feature views | [Workspace navigation](design/module-workspace.md), [compact layout, Fit and preferences](design/compact-workspace.md) |
| Assign commands, modifiers and axes; author pages and guides | [Module authoring](module-authoring.md), [axis cleanup](axis-cleanup.md), [MFD layouts](mfd-stacked-labels.md) |
| Test physical inputs or watch demonstrations | [Live input testing](live-input-testing.md), [interactive training](interactive-training.md); training is free on current main, real input testing requires Owner access |
| Work with unbundled hardware and drawings | [Generic hardware](generic-hardware.md), [custom drawings, replacement and deletion](custom-mappings.md); drawings retain the underlying hardware edition limits |
| Customize appearance | [Skins](design/themes.md), [per-window backgrounds and artwork opacity](design/appearance-editor.md); Owner access required |
| Deploy and remove files | [Fly Now and Clear](fly-now.md) |
| Back up, restore and isolate test data | [Database and recovery](operations/database.md), [test profiles and reset](operations/test-profiles.md) |
| Diagnose problems | [Logs](logging.md), [support-report privacy and acceptance](distribution-readiness.md); F1 opens offline workflow help |

The Free release supports its explicit reviewed Thrustmaster catalog and a fixed photographic Navy/Amber preset. Current signed Owner activation unlocks implemented capabilities. Customer Premium licensing/payment is not available; availability and pricing remain TBA. Free training is implemented on current main, after the documented v1.1.1.0 release baseline; see the training guide for release availability. The edition guide is authoritative when an advanced workflow example uses reserved hardware or features. Importing artwork does not itself change the device’s allowed hardware identity.

## Build, validate and release

- [Contributing](../CONTRIBUTING.md), [local test scripts](../tests/README.md), [80/80/50 quality gates](development/quality-gates.md).
- [Architecture](architecture/README.md), [terminology](architecture/terminology.md), [SQLite/snapshot decision](adr/0001-local-sqlite-and-immutable-snapshots.md).
- [Tested application releases](application-release.md), [candidate record and acceptance rules](release-acceptance.md), [pending distribution/source review](distribution-review.md).
- [Shared UI defaults, reviewed adoption and output](migration/shared-default-workspace.md), [developer draw.io exporter](drawio-hardware-workflow.md).

## Historical evidence and design prototypes

[Application completion](migration/application-completion.md) records the delivered migration scope. #83, #9 and #111 are closed; current acceptance work is indexed by [#245](https://github.com/ScottyMac52/DCS-IPIA/issues/245). Real-device/DCS observations, older-release recovery/upgrade evidence and distribution decisions remain pending where no results are recorded.

The Phase 1 status, repository inventory, domain-schema-v1, configuration-snapshot-v1 and generated Common capability inventory retain their original baseline/version and evidence. Their historical pending work is not the current backlog. Do not rewrite pinned source hashes, captured consumer documents or fixture release versions to resemble the application release.

The [workspace concept images and HTML mockups](design/workspace-layout-concepts/README.md) and [assignment editor prototype](ux/assignment-editor-approved.html) are design references, not application screenshots or complete feature specifications. Current navigation and layout are described above.

War Thunder: [native foundation and readiness](war-thunder-adapter.md), [PR6 shared-contract audit](design/shared-war-thunder-pr6.md). Desktop integration follows the verified native backend contract.
