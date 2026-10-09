# Cougar MFD labels with colored layers

Import `tests/Dcs.Ipia.Tests/Fixtures/CustomMappings/Cougar-MFD-Stacked.drawio` using Hardware → Import drawio mapping, select the verified 28-button Cougar definition, then review and apply the revision to the desired module profile. Repeat explicitly for other MFD instances. Existing bindings, modifiers and tuning remain in the module. This PR does not replace the bundled hardware catalog.

Left and right callouts contain horizontal lines, read left to right. Top and bottom callouts contain upright stacked letters, read top to bottom. Each assigned layer/modifier adds an adjacent column. Spaces remain blank rows. The center Cougar graphic is covered by an editable white panel. LEFT/RIGHT headings use stacked letters; TOP/BOTTOM headings remain horizontal. All 28 physical inputs retain explicit `JOY_BTN1`–`JOY_BTN28` mappings.

The renderer retains the authored base color (dark text on this white layout), the existing modifier colors and the existing UI Layer colors. Its legend identifies the actual used layers and modifier chords. No illustrative shift placeholders are exported. Very long labels or many layers may need larger authored callouts; stacked text fails with a larger-callout diagnostic if it cannot fit at the minimum font size.

## Opt-in drawing contract

`ipiaTextLayout=stacked` on a mapped label opts that control into upright glyph columns. `ipiaTextLayout=horizontal` uses ordinary multiline callouts. `ipiaStatic=1` on presentation labels preserves fixed headings during live assignment substitution. These draw.io style settings are editable under Edit Style. The supplied file includes them already.

New imports use exporter `ipia-drawio-3`. Persisted version-one and version-two mappings are reproduced with their original exporter; unmarked drawings retain the version-two SVG and layout behavior. The new renderer branch requires `data-ipia-text-layout="stacked"`, so existing device callouts continue using the established renderer. Arbitrary rotations, executable markup and external images remain unsupported.

The embedded diagram comes from the English “Layout of buttons,” printed page 8 of the [Thrustmaster MFD Cougar manual](https://ts.thrustmaster.com/download/accessories/pc/MFD/MFD_COUGAR_Pack_User%20Manual.pdf), selected by Scott McIntosh on 2026-10-09. Manufacturer artwork provenance and distribution approval remain tracked in issue #183; this fixture does not automatically add it to the bundled catalog.
