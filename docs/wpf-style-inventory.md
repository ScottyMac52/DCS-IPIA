# WPF style inventory

Scope: 52 XAML files, including theme dictionaries, and 141 C# files under `src/Heimdall.App`. Counts below describe declaration sites in application XAML and direct C# construction of types also declared in XAML. Templates and repeaters can create many runtime instances per declaration. Implicit means no inline Style attribute. It includes controls styled by type, styles supplied by a parent or container, and layout elements that need no style.

## Resource policy

- ComboBox and AssignmentInputPicker declarations use `AppComboBox`. Code-created picker controls select the same resource through `SetResourceReference`. The implicit ComboBox style also provides a solid themed face as a fallback.
- TextBox and CheckBox have shared type styles and optional `AppTextBox` and `AppCheckBox` variants. Buttons use the implicit button style or a role style such as `ActionButton`, `ToolbarActionButton`, or `GameTile`.
- Collections, scrollbars, menus, sliders and other shared controls can receive their style by type. Specialized control templates own their internal parts. In particular, the ComboBox editable text part intentionally has no standalone TextBox style.
- Palette brushes use dynamic resource references so palette and accessibility changes can propagate. A control using a shared static style can still appear translucent if its brush is translucent. The combo box face and popup now use the solid control brush. Disabled, hover, and focus visuals remain themed.
- This is a source inventory. Verify rendered states on Windows with `tests/smoke-tests.ps1` and the Elite Devices drawing picker over artwork.

| Type | XAML explicit Style | XAML implicit or layout | Direct C# constructions |
| --- | ---: | ---: | ---: |
| `ActionBar` | 0 | 20 | 0 |
| `AdornerDecorator` | 0 | 3 | 0 |
| `Application` | 0 | 1 | 0 |
| `AssignmentInputPicker` | 4 | 0 | 0 |
| `Binding` | 0 | 2 | 7 |
| `Border` | 3 | 11 | 1 |
| `Button` | 144 | 99 | 23 |
| `Canvas` | 0 | 7 | 1 |
| `CheckBox` | 6 | 7 | 5 |
| `ColumnDefinition` | 0 | 48 | 5 |
| `ComboBox` | 76 | 0 | 4 |
| `ComboBoxItem` | 0 | 68 | 0 |
| `DcsCommandsManagementView` | 0 | 2 | 0 |
| `ContentControl` | 0 | 6 | 0 |
| `ContentPresenter` | 0 | 1 | 0 |
| `ContextMenu` | 0 | 1 | 3 |
| `ContrastOutlineBorder` | 0 | 1 | 2 |
| `ControlTemplate` | 0 | 2 | 0 |
| `DataGrid` | 0 | 19 | 0 |
| `DataGridCheckBoxColumn` | 0 | 4 | 2 |
| `DataGridTextColumn` | 0 | 64 | 1 |
| `DataTemplate` | 0 | 10 | 1 |
| `DataTrigger` | 0 | 3 | 0 |
| `DeviceDetailsLayout` | 0 | 4 | 0 |
| `DockPanel` | 0 | 40 | 13 |
| `EliteAssignmentArtworkView` | 0 | 1 | 0 |
| `Ellipse` | 0 | 2 | 0 |
| `Expander` | 0 | 23 | 0 |
| `FeatureActionBar` | 0 | 4 | 0 |
| `GameAssignmentEditorView` | 0 | 4 | 0 |
| `GameCommandBrowserView` | 0 | 3 | 0 |
| `GameControllerSetup` | 0 | 1 | 0 |
| `GameControllerWorkspace` | 0 | 3 | 0 |
| `GameDrawingView` | 0 | 3 | 0 |
| `GameFeatureNavigation` | 0 | 3 | 0 |
| `GameSelectionView` | 0 | 1 | 0 |
| `GameWorkspaceShell` | 0 | 4 | 0 |
| `GameWorkspaceSplit` | 0 | 4 | 0 |
| `Grid` | 0 | 48 | 2 |
| `GridSplitter` | 4 | 3 | 2 |
| `GroupBox` | 8 | 6 | 0 |
| `GroupStyle` | 0 | 1 | 0 |
| `Image` | 0 | 5 | 8 |
| `ItemsControl` | 0 | 3 | 0 |
| `ItemsPanelTemplate` | 0 | 3 | 1 |
| `Label` | 0 | 11 | 2 |
| `ListBox` | 4 | 14 | 3 |
| `ListBoxItem` | 0 | 23 | 2 |
| `Menu` | 0 | 1 | 0 |
| `MenuItem` | 0 | 34 | 9 |
| `Path` | 0 | 4 | 0 |
| `Popup` | 0 | 1 | 0 |
| `PriorityBinding` | 0 | 1 | 0 |
| `RadioButton` | 0 | 2 | 2 |
| `ResourceDictionary` | 0 | 7 | 2 |
| `RowDefinition` | 0 | 82 | 6 |
| `ScrollViewer` | 0 | 38 | 10 |
| `Separator` | 0 | 1 | 3 |
| `Setter` | 0 | 22 | 3 |
| `Slider` | 0 | 11 | 0 |
| `StackPanel` | 0 | 119 | 25 |
| `Style` | 0 | 9 | 1 |
| `TabControl` | 0 | 1 | 1 |
| `TabItem` | 0 | 6 | 1 |
| `TextBlock` | 131 | 170 | 40 |
| `TextBox` | 33 | 35 | 12 |
| `Thumb` | 0 | 1 | 0 |
| `ToggleButton` | 0 | 1 | 0 |
| `TreeView` | 0 | 1 | 0 |
| `Trigger` | 0 | 3 | 0 |
| `UniformGrid` | 0 | 1 | 0 |
| `UserControl` | 0 | 26 | 0 |
| `Viewbox` | 0 | 5 | 0 |
| `WarThunderNativeEditorView` | 0 | 1 | 0 |
| `Window` | 0 | 17 | 0 |
| `WorkspaceDrawingPreview` | 0 | 4 | 0 |
| `WrapPanel` | 0 | 59 | 6 |
