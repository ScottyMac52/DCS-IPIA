# Flight Patch artwork

Created for DCS-IPIA on 2026-10-16 at Scott McIntosh's direction. This is a new vector interpretation of the Flight Patch concept in the earlier DCS-IPIA visual-direction board: a navy/gold circular aviation badge, winged joystick, generic aircraft, star and DCS-IPIA lettering. The concept-board raster is not cropped, traced or redistributed. Shapes and scenery are authored in `scripts/generate_flight_patch.py`; no manufacturer photos, service insignia or third-party logos are included.

The vector geometry and generated application artwork are part of this repository under its MIT license. Lettering is outlined from the existing bundled `DejaVuSans-IPIA.ttf` bold font; retain the DejaVu notice already shipped in `notices/DejaVuSans-IPIA.LICENSE.txt`. This note is shipped as `notices/flight-patch-PROVENANCE.md`.

`flight-patch.svg`, `flight-patch-compact.svg`, `flight-patch-banner.svg` and `setup-sidebar.svg` are scalable, standalone masters. Text is outlined, so rendering does not depend on installed fonts. The compact variant removes the aircraft, lettering and star for 16–48 px icons. The ICO contains 16, 20, 24, 32, 40, 48, 64, 128 and 256 px images with transparency. Larger frames use the complete badge. PNGs are embedded WPF resources; BMPs are 24-bit Inno Setup artwork. No palette changes recolor these identity assets.

Regenerate from the repository root using Python 3 with Pillow and fontTools, plus Inkscape on PATH:

```sh
python scripts/generate_flight_patch.py
python scripts/check_branding_assets.py
```

The generator uses the bundled font, writes all vector/raster derivatives and records input/output SHA256 values in `manifest.json`. App builds consume the committed assets and do not require these authoring tools. Review full-size, 16/32 px and 150–200% title-bar/taskbar results when revising the design. Manual Windows display acceptance remains in #191; the brand decision and later enhancements remain tracked in #246.
