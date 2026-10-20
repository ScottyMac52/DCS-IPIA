# Theme resources for the module workspace

The workspace loads one of four built-in palettes: `Themes/Light.xaml` (Modern Light), `Themes/Dark.xaml` (Modern Dark), `Themes/NavyAmber.xaml` (Navy/Amber), or `Themes/CockpitGreen.xaml` (Cockpit Green) through the application resource dictionary. All palettes define the same semantic brush keys (`WindowBackgroundBrush`, `PrimaryTextBrush`, `SurfaceBrush`, `AccentTextBrush`, etc.). Shared reusable component styles (`Card`, `Hint`, `NavigationItem`, `SampleControl`) live in `Themes/Styles.xaml`. `MainWindow.xaml` refers to them through `DynamicResource` and contains no color literals.

With Owner access, the main-window picker and Settings apply a selected skin immediately and persist its identity in the profile SQLite preferences. Custom appearance values/images live in the profile appearance folder. Neither becomes part of a per-module portable snapshot. Free uses a fixed Navy/Amber preset and retains saved preferences without applying customization.

To add a built-in theme, append its identity to `AppearanceTheme`, create a palette dictionary with the same name, keys and appropriate contrast, then expose it in both pickers and the resource-contract check. Retain the existing enum order/Light and Dark names for preference compatibility. Replacing the merged palette dictionary at runtime updates the view because the brush and style references are dynamic. The page layout and application services do not change.

The current default light colors and sizing are preserved. Dark provides contrasting backgrounds, labels, selected navigation, and disabled control states. Check keyboard focus visibility and contrast across every future screen when introducing another palette.

Shared TabControl surfaces and Button, TabItem (including NavigationItem), ComboBox and ComboBoxItem templates bind their painted surfaces to semantic palette brushes. Selected, hovered/pressed, disabled and keyboard-focus states must stay in the active palette; setting only a control's Background/Foreground does not prevent a Windows default template from painting light selected/disabled chrome. Dropdowns keep their native ComboBox selection/keyboard behavior and required PART_Popup/PART_EditableTextBox parts. Expander and GroupBox headers explicitly use the active text brush.

The Windows ThemeControlSmoke renders actual selected tabs, disabled buttons/comboboxes, popup rows and expander text, then switches the same open controls Modern Dark → Navy/Amber → Cockpit Green → Modern Light → Modern Dark. It checks painted pixels and rendered text, focus contrast, arrow/F4 dropdown keys and editable text synchronization. Theme screenshots accompany the existing custom-mapping visual-review artifact in CI. These checks supplement manual high-contrast/scaling review.

## Built-in skins and legacy styles

The Light/Dark persisted identities and colors remain compatible; their UI names are Modern Light and Modern Dark. Navy/Amber uses navy surfaces with warm amber accents; Cockpit Green uses charcoal/green surfaces with readable neutral text and green accents. Settings offers **Restore default appearance**, which applies/persists Modern Light without changing the occupied-destination policy or Saved Games folder. The selected palette itself is the live preview; no separate confirmation is needed. The scrollable settings content stays reachable at smaller display sizes.

`Controls.xaml` keeps its existing named styles and spacing but uses DynamicResource for every palette brush. Static references to base styles are deliberate; palette references must be dynamic so open controls follow replacement palettes. ThemeControlSmoke now includes AppTextBox, AppComboBox, AppLabel and AppCheckBox alongside selected/disabled/focused controls and dropdown behavior across all four skins. Settings and main-window smoke cover selection, reopen/startup, immediate application and reset. File and real SQLite tests verify skin round trips, profile isolation and preservation of other preferences.

Run `python3 scripts/check_theme_resources.py` for palette parity and unresolved dynamic resources throughout application XAML; the checker also rejects fixed brush references in shared theme dictionaries. Existing Windows smoke captures each skin in the custom-mapping-preview CI artifact. Manual keyboard/high-contrast/scaling acceptance remains in #191. Generated kneeboards, assignment/modifier legend colors and immutable snapshots are independent of the application palette.

Flight Patch branding/icons, custom accents and imported palettes are implemented; see [branding provenance](../../src/Heimdall.App/Branding/PROVENANCE.md) and [appearance customization](appearance-editor.md). No arbitrary XAML skin loading is introduced.

## Main window and menus

The main-window client area, menu bar, menu/context popups, separators and menu-item highlight/disabled/focus/check/submenu states share the active palette. Menus.xaml supplies painted templates while preserving WPF MenuItem roles, PART_Popup, routed commands, access keys, input-gesture text and keyboard navigation. Already-open popups update with the same dynamic brushes. The main theme selector also refreshes on activation after a workspace changes appearance.

The native main-window title bar uses Windows 11 DWM caption/text/border colors from the palette, applied after handle creation and on live skin changes. Standard Windows caption buttons, dragging, resizing and system menus remain native. Older Windows retain system chrome; native high-contrast mode resets caption colors to system defaults on application. Server/headless desktops that reject DWM caption inspection report it as unsupported in smoke; unexpected failures and supported color mismatches still fail. Unsupported DWM attributes do not prevent startup. See Microsoft's DWMWINDOWATTRIBUTE documentation: https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute.

MainWindowThemeSmoke renders the actual main window/menu under all four skins, switches while a submenu is open, checks disabled commands, nested/checkable/context items, keyboard Down/Enter and routed command execution, and reads Windows 11 caption attributes. Main/menu screenshots are added to the custom-mapping-preview artifact.

## Application-wide consistency

Primitives.xaml and Collections.xaml provide the shared styles for all controls used by the app: generated DataGrid cell editors/checks and resize/sort headers, lists, trees, checkboxes, text boxes, sliders, scrollbars, expanders, tooltips and labels. Removed module/training-only collection overrides so code-created hardware/drawing/recovery/help dialogs use the same styles. System brush resources in each palette also cover stock control details. ErrorTextBrush supplies readable validation text in every skin.

ApplicationTheme registers window-loaded handling before any UI is shown, applying live root brushes and Windows 11 caption settings to every app-owned window, including temporary label/preview/confirmation windows. Explicit per-column styles are preserved; default generated table checkboxes remain display-only until editing. Dynamic assignment callouts and drag labels use palette brushes; authored graphics, exported kneeboards and live modifier/legend colors retain their independent meaning.

ThemeMessageBox preserves explicit confirmation defaults and return results with app-owned modal dialogs. Closing a Yes/No prompt returns No; Cancel remains Cancel. An OS message box is retained only for bootstrap/logging failures before WPF resources exist. Open/save/folder browsing uses the app-owned themed browser. Bootstrap dialogs and other operating-system surfaces follow Windows appearance.

ApplicationThemeSmoke keeps real axis tuning, command usage, support report, help and settings windows open while switching all skins. It checks shared rendered collections, committing a table edit, generated checkbox styling, keyboard slider changes, scrolling, validation colors and four confirmation button/result combinations. Existing hardware/drawing/recovery/training smoke continues to cover their real workflows. Resource checks reject new fixed XAML colors, direct unthemed message boxes and missing shared control styles.

## Personal appearance overrides

Settings → Customize appearance provides live background, accent and validated palette previews with Apply/Cancel and profile-local persistence. See [the appearance editor guide](appearance-editor.md) for image budgets, readability safeguards, the palette format and backup/reset behavior.
