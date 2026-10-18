namespace Dcs.Ipia.Application
{
    /// <summary>Stable appearance identities, independent of translated captions and module names.</summary>
    public static class AppearanceTargets
    {
        #region BEGIN Public
        /// <summary>Gets every supported window, dialog and workspace view.</summary>
        public static IReadOnlyDictionary<string, string> All { get; } = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MainWindow"] = "Main / modules",
            ["ModuleWorkspaceWindow"] = "Module workspace",
            ["NewModuleWindow"] = "New module",
            ["SharedUiLayerWorkspaceWindow"] = "Shared UI layer workspace",
            ["SettingsWindow"] = "Settings",
            ["AppearanceWindow"] = "Appearance preview",
            ["HardwareInventoryWindow"] = "Hardware inventory / setup",
            ["UserMappingsWindow"] = "Hardware drawings",
            ["MappingRevisionReviewWindow"] = "Drawing adoption / rollback review",
            ["SavedGamesImportWindow"] = "Saved Games import",
            ["CommandImportReviewWindow"] = "Command import review",
            ["IntegrationImportReviewWindow"] = "Integration instructions review",
            ["CommandUsageWindow"] = "Command usage",
            ["AxisTuningWindow"] = "Axis tuning",
            ["AxisDefaultsReviewWindow"] = "Axis defaults review",
            ["UserDataProfileWindow"] = "Profiles / reset",
            ["UserDataRecoveryWindow"] = "Data / recovery",
            ["SupportReportWindow"] = "Support report",
            ["WorkflowHelpWindow"] = "Help",
            ["TrainingWindow"] = "Training",
            ["BrowseWindow"] = "Open / save / folder browser",
            ["NewFolderDialog"] = "New folder",
            ["MessageDialog"] = "Messages / confirmations",
            ["ChangeLabelDialog"] = "Change label",
            ["SearchReplaceDialog"] = "Search / replace labels",
            ["CurrentKneeboardDialog"] = "Current kneeboard",
            ["PagePreviewDialog"] = "Page preview",
            ["DevicesView"] = "Workspace — Devices",
            ["ModifiersView"] = "Workspace — Modifiers",
            ["CommandsView"] = "Workspace — Commands",
            ["AssignmentsView"] = "Workspace — Assignments",
            ["PagesView"] = "Workspace — Pages",
            ["InputTestView"] = "Workspace — Input Testing",
            ["KneeboardsView"] = "Workspace — Kneeboards",
            ["DocumentationView"] = "Workspace — Documentation",
        });
        /// <summary>Resolves a view override, then its owning window, then the application default.</summary>
        public static AppearanceSettings Resolve(AppearanceSettings settings, string target, string? parent = null)
        {
            if (settings.Backgrounds?.TryGetValue(target, out var selected) == true) return selected.Apply(settings);
            if (parent is not null && settings.Backgrounds?.TryGetValue(parent, out selected) == true) return selected.Apply(settings);
            return settings;
        }
        /// <summary>Enumerates managed image references that must survive a successful save.</summary>
        public static IEnumerable<string> Images(AppearanceSettings settings) =>
            new[] { settings.Image }.Concat(settings.Backgrounds?.Values.Select(x => x.Image) ?? []).OfType<string>().Distinct(StringComparer.Ordinal);
        #endregion END Public
    }
}
