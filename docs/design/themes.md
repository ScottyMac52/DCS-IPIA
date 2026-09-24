# Theme resources for the module workspace

The workspace loads `Themes/Light.xaml` or `Themes/Dark.xaml` through the application resource dictionary. Both palettes define the same semantic brush keys (`WindowBackgroundBrush`, `PrimaryTextBrush`, `SurfaceBrush`, `AccentTextBrush`, etc.). Shared reusable component styles (`Card`, `Hint`, `NavigationItem`, `SampleControl`) live in `Themes/Styles.xaml`. `MainWindow.xaml` refers to them through `DynamicResource` and contains no color literals.

The header picker applies Light or Dark immediately. The choice is stored in the current user's local application data under `DCS-IPIA/theme.txt` and restored on the next launch. If that file cannot be read, the app opens in Light; if a choice cannot be saved, it remains active for the session and displays a message. Appearance preferences do not enter the module database or immutable snapshots.

To add a theme, create another palette dictionary with the same keys and appropriate contrast, then expose it in the picker. Replacing the merged palette dictionary at runtime updates the view because the brush and style references are dynamic. The page layout and application services do not change.

The current default light colors and sizing are preserved. Dark provides contrasting backgrounds, labels, selected navigation, and disabled control states. Check keyboard focus visibility and contrast across every future screen when introducing another palette.
