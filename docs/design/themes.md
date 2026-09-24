# Theme resources for the module workspace

The workspace loads `Themes/Light.xaml` through the application resource dictionary. Semantic brush keys (`WindowBackgroundBrush`, `PrimaryTextBrush`, `SurfaceBrush`, `AccentTextBrush`, etc.) and reusable component styles (`Card`, `Hint`, `NavigationItem`, `SampleControl`) live there. `MainWindow.xaml` refers to these through `DynamicResource` and contains no color literals.

To add a theme, create another dictionary with the same keys and appropriate contrast, then select that dictionary as the active theme. Replacing the merged theme dictionary at runtime updates the view because its brush and style references are dynamic. The page layout and application services do not change. Future theme selection and persistence can be added independently when settings are implemented.

The current default light colors and sizing are preserved. This PR adds theme readiness; it does not expose a theme picker or change the reviewed layout. Check keyboard focus visibility, disabled states, and contrast when introducing a new palette.
