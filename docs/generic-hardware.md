# Generic controllers and empty defaults

An adopted inventory unit can be used without bundled artwork. In **New module**, leave **Start with empty defaults** checked, choose a unit from **Adopted hardware inventory**, and add each physical unit to the reviewed list. The same reusable definition can be used for repeated units; their actual GUID-qualified native filenames and module profiles remain distinct. Offline units retain the last explicitly adopted controls. Discovery failures do not invent a definition.

For an existing module, open **Devices**, select the aircraft or module UI Layer scope, choose an adopted inventory unit, and click **Add inventory unit with generic controls**. Add the same physical unit to both scopes to share its physical profile. Save the staged change. Existing assignments and deliberately pinned shared revisions are preserved.

The image-free editor offers only supported buttons, absolute axes and all eight directions of each supported POV. Unsupported objects remain inventory diagnostics. Drag/drop, right-click assignment, modifier editing, axis tuning and live testing use the established editor flows. Transfers stay within one device and scope; repeated units have independent assignments. **Generic control name** edits the selected profile's physical control label. **Displayed label** edits the assignment's caption for the selected scope and chord. Neither changes the native DCS input key or other units.

**Rendered Kneeboard** shows every current draft page in a scrollable preview. The **Kneeboard** tab renders saved state; its page selector shows each generated page, including the `-002.png` and later filenames used in the package. Generic output uses a fixed 22-pixel body font on 1200×1600 pages. Labels wrap and continue across pages rather than shrinking at high control counts. Every row names its input, editable control label, scope, chord and effective caption. Modifier descriptions are included after the controls. Ordering uses control kind and native instance, with stable POV direction order.

Create the authored overview/summary page in **Pages**, as required by the existing package workflow. Import or author the aircraft command catalog, save, export a module snapshot, and build OvGME or a complete release. Documentation lists every generated generic page and preserves native inputs, modifier references and sparse axis filters. Generic pages do not use an image or a path on the original computer.

## Definition, identity and portability contracts

- Inventory schema 1 and database schema 8 retain the reusable, content-addressed native capability definition independently from physical inventory units and module profiles.
- A module pins its generic definition into the existing hardware catalog source and each profile's optional `GenericCapabilities` diagram member. The layout path is a logical `generic/<definition-id>.svg` identity, never a filesystem dependency. Supported control geometry and image-free SVG are regenerated deterministically and verified against the capability evidence before save, reopen, snapshot or export. A forged input, relative axis, missing capability evidence or foreign background is rejected.
- The existing editor payload version 3 and module snapshot schema 1 accept the additive optional member. Old bundled snapshots omit it and retain their canonical representation. No SQL migration or mutation of existing modules is needed. Portable snapshots carry all controls, labels, geometry and evidence; restoring needs neither an inventory database nor a device connection.
- Inventory capability adoption affects future additions. Existing module definitions remain pinned. Rebinding an existing profile is still an explicit action and retains labels, assignments, filters and modifier references. Built-in catalog reload preserves generic definitions. Compatible bundled artwork can be selected explicitly; mismatched control sets are rejected for inventoried units in module creation.
- Empty defaults are an explicit module creation choice recorded in `config/ipia-default-choice.json`. They retain the shared UI command palette but import no controller profiles, assignments or modifier activators. No shared revision is pinned. Unchecking the choice copies the deliberately selected saved revision using the existing verified baseline and rebind workflow. Existing modules and shared-default repositories are unchanged.

## Validation and physical acceptance

Automated fixtures cover repeated units; aircraft and UI membership; assignment move/copy; buttons, axes and POV directions; hold/toggle modifiers; sparse filters; independent labels; SQLite reopening; image-free pagination at 162 actual controls; snapshot restore into a database without inventory; documentation; deterministic ZIP generation; and rejected malformed evidence. Windows smoke covers inventory selection in module creation, the default empty choice, scope addition, saved page selection and reopening.

With an unbundled physical controller:

1. Discover/rescan, inspect supported and unsupported controls, and explicitly adopt the unit. Repeat with two identical units if available.
2. Create an empty module using both units. Import the correct aircraft command catalog; assign a button, axis and POV direction. Create a modifier and shifted binding, tune the axis, and name a control. Repeat in UI Layer where the DCS command supports that input kind.
3. Live-test state changes, save/reopen, and compare every draft/saved generic page. Add the overview page, export a snapshot, restore into a separate empty profile, and compare output.
4. Build the package, enable it through OvGME, then verify the correct device columns, GUID-qualified inputs, modifier switches, axis filters and all kneeboard pages inside DCS. Disable the package to restore the prior files.

Automatic first-run onboarding and custom artwork authoring/import remain outside this feature.
