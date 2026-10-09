# Theme resources for the module workspace

The workspace loads `Themes/Light.xaml` or `Themes/Dark.xaml` through the application resource dictionary. Both palettes define the same semantic brush keys (`WindowBackgroundBrush`, `PrimaryTextBrush`, `SurfaceBrush`, `AccentTextBrush`, etc.). Shared reusable component styles (`Card`, `Hint`, `NavigationItem`, `SampleControl`) live in `Themes/Styles.xaml`. `MainWindow.xaml` refers to them through `DynamicResource` and contains no color literals.

Settings applies Light or Dark immediately and persists the appearance preference for the selected user-data profile. Appearance preferences do not enter the module database or immutable snapshots.

To add a theme, create another palette dictionary with the same keys and appropriate contrast, then expose it in the picker. Replacing the merged palette dictionary at runtime updates the view because the brush and style references are dynamic. The page layout and application services do not change.

The current default light colors and sizing are preserved. Dark provides contrasting backgrounds, labels, selected navigation, and disabled control states. Check keyboard focus visibility and contrast across every future screen when introducing another palette.

Shared TabControl surfaces and Button, TabItem (including NavigationItem), ComboBox and ComboBoxItem templates bind their painted surfaces to semantic palette brushes. Selected, hovered/pressed, disabled and keyboard-focus states must stay in the active palette; setting only a control's Background/Foreground does not prevent a Windows default template from painting light selected/disabled chrome. Dropdowns keep their native ComboBox selection/keyboard behavior and required PART_Popup/PART_EditableTextBox parts. Expander and GroupBox headers explicitly use the active text brush.

The Windows ThemeControlSmoke renders actual selected tabs, disabled buttons/comboboxes, popup rows and expander text, then switches the same open controls Dark → Light → Dark. It checks painted pixels and rendered text, focus contrast, arrow/F4 dropdown keys and editable text synchronization. Theme screenshots accompany the existing custom-mapping visual-review artifact in CI. These checks supplement manual high-contrast/scaling review.
