# Personal appearance and background previews

Open **Settings → Customize appearance**. Choose a local JPG/PNG and adjust image opacity, fill/fit, horizontal/vertical alignment, theme blending, panel opacity and independent control opacity. The open application windows preview changes immediately. **Apply** commits the accepted appearance. **Cancel**, Escape or closing the editor restores the last saved appearance. A failed Apply restores the accepted appearance and leaves the editor open with an actionable status so you can retry.

Skin choice in Settings/the main menu retains its existing immediate-save behavior. The advanced editor works with that selected skin. **Clear customization** previews removal of the image, accent and palette while keeping the chosen built-in skin; Apply confirms it. Settings' **Restore default appearance** clears custom appearance and returns to Modern Light. Assignment/drop and Saved Games preferences are independent.

## Image controls and readability

Images are limited to single-frame JPG/PNG, 20 MiB encoded, 8192 pixels per side and 24 million source pixels. Decode inspects dimensions before materializing pixels, downsamples widths above 2560 pixels, releases the original file and normalizes accepted content as a metadata-free PNG. The managed PNG must also fit 20 MiB. The image isn't copied into the profile during preview; Apply commits it. Moving/deleting the original after choosing it does not invalidate the decoded preview or saved background.

Requested image opacity is 0–30%. Theme blending mixes the photograph toward the skin's surface color. **Fill** preserves aspect ratio while cropping to fill; **Fit** preserves the entire image with skin-colored letterboxing. Position controls determine which subject stays visible as the window resizes. The photograph is composed once per window, so cards and the menu bar reveal the same continuous image rather than repeating it per control. Panel and status-bar opacity allow 0–100% (35% by default), with contrast protection against the softened image; previously saved opacity values are preserved. **Control opacity** independently adjusts normal button, dropdown, text-field, checkbox, list, table, tree and tab backgrounds from 0–100%, defaulting to 100%. Existing settings that omit this field retain opaque control fills. Control opacity also works without a photograph; panels retain their own setting. Text, icons, borders and focus indicators stay opaque. Hover/pressed, selected and disabled states retain protected palette fills. Open popup menus, dropdowns and tooltips keep an opaque base, so they never reveal the desktop behind their separate popup window. Contrast protection may raise effective control opacity; it tests control foregrounds against the softened window image and possible enclosing surfaces. No whole-control Opacity is applied.

Readability protection may reduce effective image opacity or increase panel/control opacity. It conservatively tests both black/white photographic extrema against the primary/secondary/accent/error text colors and requires at least 4.5:1 for decoration compositing whenever the base colors support it. Imported palettes must independently meet 4.5:1 for documented text/background pairs. Windows high contrast suppresses image decoration and custom palette/accent overrides; it does not erase saved choices. The full real-Windows high-contrast/mixed-DPI matrix remains in #191.

Flight Patch retains its identity colors. This customization paints app-owned window client areas and shared surfaces; authored source drawings, input-test modifier/legend colors, module data and exported kneeboards remain independent. Operating-system dialogs follow Windows appearance.

## Accents and imported palettes

An accent is an opaque `#RRGGBB` value, or blank to retain the skin default. It changes accent text/focus; selection colors come from the base/imported palette. Accent colors must meet 4.5:1 against window and panel backgrounds. An accent incompatible with a newly selected skin falls back to the skin accent with a diagnostic; its saved value remains available to edit.

Palette imports are UTF-8 JSON, maximum 64 KiB, with exactly `SchemaVersion: 1` and a `Colors` object containing the 19 semantic keys in [the example palette](appearance-palette-example.json). Every color is opaque `#RRGGBB`. Unknown/duplicate keys, partial palettes, unsupported versions, malformed values and unreadable pairs are rejected before replacing the working preview. There is no XAML, script, external resource or image-path interpretation in the palette format. Imported colors are saved by value, so the source JSON can move later.

Text/background pairs: primary and secondary text on the window and panels; accent and error text on the window and panels; control foreground/background; primary text on selected backgrounds; disabled text/background; status text/background. Existing built-in skins remain available after any import failure. Use skin palette/clear customization/reset for recovery.

## Persistence and recovery

`appearance/settings.json` is versioned independently of existing preferences and uses a safe relative, SHA256-addressed PNG name. Accepted PNGs live beside it under the current profile. JSON commits use a temporary file and atomic replacement; failed saves preserve the prior in-memory/persisted snapshot. Newly staged images are removed on a failed commit. Unused managed image versions are removed after a successful commit. No absolute source-image paths are persisted.

Full profile backup/restore already includes this tree; automated coverage now verifies settings and PNG bytes survive an actual verified SQLite profile backup/restore. Profiles remain isolated. Missing/tampered/oversized images fall back to the built-in background and surface a diagnostic. Malformed/unsupported preferences fall back without rewriting the original evidence. Reset or choosing the image again repairs the supported cases.

Regression coverage: `AppearanceValidationTests`, `FileAppearanceStoreTests` and the full-backup test. Windows `AppearanceEditorSmoke` exercises all skins, cross-window live preview, malformed accent rejection, Apply/Cancel/window-close rollback, original-file deletion, persistence/reopen, palette import and failed-save rollback. Control-opacity smoke samples rendered text-field pixels at opaque/translucent settings, checks opaque text/borders, focus, selected/disabled states and popup bases, then verifies Apply/reopen and Cancel/close rollback in all four skins. Synthetic background screenshots are included in the standard CI visual artifact; user photos are not bundled in this PR.

## Window and workspace backgrounds

In **Apply background to**, choose **Application default**, a window/dialog, or a workspace view. Targets without an override inherit the default; workspace views first inherit their owning workspace window. Choose **Custom background** and a JPG/PNG for an override, **No background image** to suppress inheritance, or **Use inherited background** to remove the override. The sample preview shows the selected target, including targets that are not currently open. Image opacity, theme blending, fit, alignment and controller artwork opacity belong to the selected target. Panel/control opacity, accent and palette remain global. Apply saves every edited target together; Cancel restores all of them.

**Controller artwork opacity** softens preview graphics independently of application backgrounds. It never applies whole-view opacity: callout text, legends and live highlight frames stay opaque. Assignments and Input Test retain transparent canvas space. Device and generic kneeboard previews can use transparent page space when a usable background is active; summary previews use a transparent outer page and a translucent reading panel. Embedded controller photographs may contain opaque pixels; use artwork opacity to see through those pixels. This does not remove their original backgrounds or edit the drawing. Exported/packaged kneeboards retain their original opaque page and full artwork.

Windows high contrast suppresses decorative images and restores full artwork opacity. Free retains its fixed preset; per-target customization remains available only under the existing entitlement.

| Targets | Windows and dialogs |
| --- | --- |
| Main workflow | Main/modules; module workspace; new module; shared UI layer workspace |
| Preferences | Settings; appearance preview |
| Hardware | Hardware inventory/setup; hardware drawings; drawing adoption/rollback review |
| Imports and commands | Saved Games import; command import review; integration instructions review; command usage |
| Axis | Axis tuning; axis defaults review |
| Support and profiles | Profiles/reset; data/recovery; support report; help; training |
| Browse | Open/save/folder browser; new folder |
| Additional dialogs | Messages/confirmations; change label; search/replace labels; current kneeboard; page preview |
| Workspace views | Devices; Modifiers; Commands; Assignments; Pages; Input Testing; Kneeboards; Documentation |

Settings use stable target IDs rather than changing titles. All referenced SHA256-addressed images remain inside the profile appearance folder and travel with full-profile backups. A successful save removes only images no target references. A failed multi-image save removes newly staged files and retains the previously accepted JSON and image set. Existing settings without target overrides or artwork opacity retain their previous appearance. Missing/corrupt images fall back to the theme surface with a diagnostic.

Regression coverage also includes `ScopedAppearanceTests`, `PreviewArtworkTests` and Windows `ScopedBackgroundSmoke`: target precedence, explicit suppression, strict override validation, multi-image retention, failed-batch rollback, preview/export separation, real target selector, Apply/reopen, Cancel and newly opened windows.
