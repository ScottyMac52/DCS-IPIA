# DCS-IPIA v1.1.1.0

This release improves the Pages editor and Free Edition snapshot import. It includes the compact workspace and the changes listed below. Published assets are identified by the tag and its TEST-EVIDENCE.json, DISTRIBUTION-EVIDENCE.json and SHA256SUMS.txt; see [GitHub Releases](https://github.com/ScottyMac52/DCS-IPIA/releases/tag/v1.1.1.0).

## Changes

- Free snapshot import offers **Remove unsupported devices and import** or **Cancel import** before inserting data. Reviewed conversion removes unsupported device memberships and dependent assignments/modifiers/pages while preserving supported content and the original snapshot. Existing saved mixed-hardware modules retain their edition restrictions. Compatible keyboard profiles can survive conversion.
- Pages hides summary-card controls on non-summary pages and hides card fields until a card is selected. Long summary descriptions expand and paginate instead of being truncated.
- The compact workspace provides eight top feature tabs, a split Assignments command/drawing layout, complete-drawing Fit, manual zoom and remembered splitter/disclosure preferences.
- Input feedback uses blue aircraft Base with contrasting edges, consistent modifier colors and a separate cyan UI Layer Base. Both training lessons use the updated palette.
- Owner appearance customization supports per-window/workspace backgrounds and independent controller-artwork opacity in previews; exported kneeboards retain their original appearance.
- Hardware setup is offered once per profile. Dismissing it suppresses repeated automatic prompts; explicit reruns preserve completion and saved data.
- Assignment-label search and replace supports reviewed previews and regex timeout recovery.

## Editions and training

Free supports the explicit reviewed Thrustmaster catalog, a fixed photographic Flight Patch/Navy-Amber appearance, supported-workspace authoring, save/build/export/deployment and backup/recovery. Existing reserved/mixed modules remain readable without permitting edits or output.

Signed offline Owner activation unlocks all implemented hardware/features, including live input testing, appearance customization and interactive training. The lessons use an isolated temporary database, a fictional generic joystick and simulated input. They currently require Owner access; free training is proposed and has not shipped. Customer Premium availability, pricing, checkout and activation remain TBA. No payment is accepted by this release. See [edition availability](https://github.com/ScottyMac52/DCS-IPIA/blob/main/docs/design/free-edition.md).

## Install, update and verify

Use setup.exe or extract the portable Windows x64 ZIP. Back up user data through **Data & recovery** before upgrading; back up DCS Saved Games inputs separately. The installer preserves user data. Review [Fly Now and Clear](https://github.com/ScottyMac52/DCS-IPIA/blob/main/docs/fly-now.md) before deployment: Clear removes recorded copied files and does not automatically restore earlier DCS controls.

Required CI checks include Linux/Windows tests, per-assembly 80% line and branch coverage, method CRAP ≤ 50 in the six measured production layers, rendered Windows smoke and exact portable/installer payload checks. Passing automation is separate from hands-on controller/DCS, older-version upgrade/recovery and display/accessibility acceptance.

Migration issues #83/#9/#111 are closed. Current acceptance is tracked in #188–#194 and #245; artwork/dependency review remains in #183/#192/#193. Publication does not mark pending observations or distribution decisions complete. Historical consumer sources, releases and pinned fixtures remain preserved.
