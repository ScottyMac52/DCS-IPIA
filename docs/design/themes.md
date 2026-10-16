# Theme resources for the module workspace

The workspace loads one of four built-in palettes: `Themes/Light.xaml` (Modern Light), `Themes/Dark.xaml` (Modern Dark), `Themes/NavyAmber.xaml` (Navy/Amber), or `Themes/CockpitGreen.xaml` (Cockpit Green) through the application resource dictionary. All palettes define the same semantic brush keys (`WindowBackgroundBrush`, `PrimaryTextBrush`, `SurfaceBrush`, `AccentTextBrush`, etc.). Shared reusable component styles (`Card`, `Hint`, `NavigationItem`, `SampleControl`) live in `Themes/Styles.xaml`. `MainWindow.xaml` refers to them through `DynamicResource` and contains no color literals.

Both the main-window picker and Settings apply a selected skin immediately and persists the appearance preference for the selected user-data profile. Appearance preferences do not enter the module database or immutable snapshots.

To add a built-in theme, append its identity to `AppearanceTheme`, create a palette dictionary with the same name, keys and appropriate contrast, then expose it in both pickers and the resource-contract check. Retain the existing enum order/Light and Dark names for preference compatibility. Replacing the merged palette dictionary at runtime updates the view because the brush and style references are dynamic. The page layout and application services do not change.

The current default light colors and sizing are preserved. Dark provides contrasting backgrounds, labels, selected navigation, and disabled control states. Check keyboard focus visibility and contrast across every future screen when introducing another palette.

Shared TabControl surfaces and Button, TabItem (including NavigationItem), ComboBox and ComboBoxItem templates bind their painted surfaces to semantic palette brushes. Selected, hovered/pressed, disabled and keyboard-focus states must stay in the active palette; setting only a control's Background/Foreground does not prevent a Windows default template from painting light selected/disabled chrome. Dropdowns keep their native ComboBox selection/keyboard behavior and required PART_Popup/PART_EditableTextBox parts. Expander and GroupBox headers explicitly use the active text brush.

The Windows ThemeControlSmoke renders actual selected tabs, disabled buttons/comboboxes, popup rows and expander text, then switches the same open controls Modern Dark → Navy/Amber → Cockpit Green → Modern Light → Modern Dark. It checks painted pixels and rendered text, focus contrast, arrow/F4 dropdown keys and editable text synchronization. Theme screenshots accompany the existing custom-mapping visual-review artifact in CI. These checks supplement manual high-contrast/scaling review.

## Built-in skins and legacy styles

The Light/Dark persisted identities and colors remain compatible; their UI names are Modern Light and Modern Dark. Navy/Amber uses navy surfaces with warm amber accents; Cockpit Green uses charcoal/green surfaces with readable neutral text and green accents. Settings offers **Restore default appearance**, which applies/persists Modern Light without changing the occupied-destination policy or Saved Games folder. The selected palette itself is the live preview; no separate confirmation is needed. The scrollable settings content stays reachable at smaller display sizes.

`Controls.xaml` keeps its existing named styles and spacing but uses DynamicResource for every palette brush. Static references to base styles are deliberate; palette references must be dynamic so open controls follow replacement palettes. ThemeControlSmoke now includes AppTextBox, AppComboBox, AppLabel and AppCheckBox alongside selected/disabled/focused controls and dropdown behavior across all four skins. Settings and main-window smoke cover selection, reopen/startup, immediate application and reset. File and real SQLite tests verify skin round trips, profile isolation and preservation of other preferences.

Run `python3 scripts/check_theme_resources.py` for palette parity and unresolved dynamic resources throughout application XAML; the checker also rejects fixed brush references in shared theme dictionaries. Existing Windows smoke captures each skin in the custom-mapping-preview CI artifact. Manual keyboard/high-contrast/scaling acceptance remains in #191. Generated kneeboards, assignment/modifier legend colors and immutable snapshots are independent of the application palette.

Branding/icon assets, custom accents and imported palettes remain follow-up work in #246. No arbitrary XAML skin loading is introduced.

## Main window and menus

The main-window client area, menu bar, menu/context popups, separators and menu-item highlight/disabled/focus/check/submenu states share the active palette. Menus.xaml supplies painted templates while preserving WPF MenuItem roles, PART_Popup, routed commands, access keys, input-gesture text and keyboard navigation. Already-open popups update with the same dynamic brushes. The main theme selector also refreshes on activation after a workspace changes appearance.

The native main-window title bar uses Windows 11 DWM caption/text/border colors from the palette, applied after handle creation and on live skin changes. Standard Windows caption buttons, dragging, resizing and system menus remain native. Older Windows retain system chrome; native high-contrast mode resets caption colors to system defaults on application. Unsupported DWM attributes do not prevent startup. See Microsoft's DWMWINDOWATTRIBUTE documentation: https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute.

MainWindowThemeSmoke renders the actual main window/menu under all four skins, switches while a submenu is open, checks disabled commands, nested/checkable/context items, keyboard Down/Enter and routed command execution, and reads Windows 11 caption attributes. Main/menu screenshots are added to the custom-mapping-preview artifact.
