namespace Heimdall.Domain
{
    /// <summary>Validates the versioned domain relationship graph.</summary>
    public sealed class VersionedDomainSchema
    {
        /// <summary>Initializes a new versioned domain schema.</summary>
        /// <param name="modules">The modules.</param>
        /// <param name="devices">The devices.</param>
        /// <param name="controls">The controls.</param>
        /// <param name="instances">The instances.</param>
        /// <param name="profiles">The profiles.</param>
        /// <param name="catalogs">The catalogs.</param>
        /// <param name="commands">The commands.</param>
        /// <param name="bindings">The bindings.</param>
        /// <param name="uiLayerUsage">The UI Layer usage.</param>
        /// <param name="uiLayerOverlays">The UI Layer overlays.</param>
        /// <param name="uiLayerTombstones">The UI Layer tombstones.</param>
        /// <param name="categories">The categories.</param>
        /// <param name="pages">The pages.</param>
        /// <param name="documentation">The documentation.</param>
        /// <param name="packages">The packages.</param>
        /// <param name="provenance">The provenance.</param>
        /// <param name="snapshot">The snapshot.</param>
        /// <param name="release">The release.</param>
        public VersionedDomainSchema(
            IEnumerable<DcsModule> modules, IEnumerable<PhysicalDevice> devices, IEnumerable<DeviceControl> controls,
            IEnumerable<DeviceInstance> instances, IEnumerable<DeviceProfile> profiles,
            IEnumerable<CommandCatalog> catalogs, IEnumerable<DcsCommandDefinition> commands,
            IEnumerable<BindingRelationship> bindings, IEnumerable<UiLayerModuleUsage> uiLayerUsage,
            IEnumerable<UiLayerOverlay> uiLayerOverlays, IEnumerable<UiLayerTombstone> uiLayerTombstones,
            IEnumerable<KneeboardCategory> categories, IEnumerable<KneeboardPage> pages,
            IEnumerable<DocumentationMetadata> documentation, IEnumerable<PackagingMetadata> packages,
            IEnumerable<SourceProvenance> provenance, SnapshotIdentity snapshot, ReleaseVersion release)
        {
            Modules = Required(modules, nameof(modules)).OrderBy(x => x.DcsId, StringComparer.Ordinal).ToArray();
            Devices = Required(devices, nameof(devices))
                          .Select(x => x with { Aliases = x.Aliases.ToArray() })
                          .OrderBy(x => x.StableKey, StringComparer.Ordinal)
                          .ToArray();
            Controls = Required(controls, nameof(controls))
                           .OrderBy(x => x.DeviceId)
                           .ThenBy(x => x.StableKey, StringComparer.Ordinal)
                           .ToArray();
            Instances = Required(instances, nameof(instances))
                            .OrderBy(x => x.ModuleId)
                            .ThenBy(x => x.StableKey, StringComparer.Ordinal)
                            .ToArray();
            Profiles = Required(profiles, nameof(profiles))
                           .OrderBy(x => x.DeviceId, StringComparer.Ordinal)
                           .ThenBy(x => x.Instance, StringComparer.Ordinal)
                           .ToArray();
            Catalogs = Required(catalogs, nameof(catalogs)).OrderBy(x => x.StableKey, StringComparer.Ordinal).ToArray();
            Commands = Required(commands, nameof(commands))
                           .OrderBy(x => x.CatalogId)
                           .ThenBy(x => x.DcsCommandId, StringComparer.Ordinal)
                           .ToArray();
            Bindings = Required(bindings, nameof(bindings))
                           .Select(x => x with { ModifierChord = x.ModifierChord.ToArray() })
                           .OrderBy(x => x.ProfileId)
                           .ThenBy(x => x.CommandId)
                           .ThenBy(x => x.ControlId)
                           .ToArray();
            UiLayerUsage = Required(uiLayerUsage, nameof(uiLayerUsage))
                               .OrderBy(x => x.ModuleId)
                               .ThenBy(x => x.CommandId)
                               .ToArray();
            UiLayerOverlays = Required(uiLayerOverlays, nameof(uiLayerOverlays))
                                  .OrderBy(x => x.ModuleId)
                                  .ThenBy(x => x.DeviceId)
                                  .ThenBy(x => x.ControlId)
                                  .ToArray();
            UiLayerTombstones = Required(uiLayerTombstones, nameof(uiLayerTombstones))
                                    .OrderBy(x => x.ModuleId)
                                    .ThenBy(x => x.StableKey, StringComparer.Ordinal)
                                    .ToArray();
            Categories = Required(categories, nameof(categories))
                             .OrderBy(x => x.Order)
                             .ThenBy(x => x.StableKey, StringComparer.Ordinal)
                             .ToArray();
            Pages = Required(pages, nameof(pages))
                        .Select(x => x with { CategoryIds = x.CategoryIds.ToArray() })
                        .OrderBy(x => x.ModuleId)
                        .ThenBy(x => x.Order)
                        .ThenBy(x => x.StableKey, StringComparer.Ordinal)
                        .ToArray();
            Documentation = Required(documentation, nameof(documentation)).OrderBy(x => x.ModuleId).ToArray();
            Packages = Required(packages, nameof(packages))
                           .Select(x => x with { IncludePatterns = x.IncludePatterns.ToArray() })
                           .OrderBy(x => x.ModuleId)
                           .ToArray();
            Provenance = Required(provenance, nameof(provenance))
                             .OrderBy(x => x.Repository, StringComparer.Ordinal)
                             .ThenBy(x => x.Path, StringComparer.Ordinal)
                             .ToArray();
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Release = release ?? throw new ArgumentNullException(nameof(release));
            Validate();
        }
        #region BEGIN Public
        /// <summary>The current version.</summary>
        public static readonly DomainSchemaVersion CurrentVersion = new(1, 0);
        /// <summary>The current migration version.</summary>
        public const int CurrentMigrationVersion = 2;
        /// <summary>The current snapshot version.</summary>
        public const int CurrentSnapshotVersion = 1;

        /// <summary>Gets the schema version.</summary>
        public DomainSchemaVersion SchemaVersion { get; } = CurrentVersion;
        /// <summary>Gets the migration version.</summary>
        public int MigrationVersion { get; } = CurrentMigrationVersion;
        /// <summary>Gets the modules.</summary>
        public IReadOnlyList<DcsModule> Modules { get; }
        /// <summary>Gets the devices.</summary>
        public IReadOnlyList<PhysicalDevice> Devices { get; }
        /// <summary>Gets the controls.</summary>
        public IReadOnlyList<DeviceControl> Controls { get; }
        /// <summary>Gets the instances.</summary>
        public IReadOnlyList<DeviceInstance> Instances { get; }
        /// <summary>Gets the profiles.</summary>
        public IReadOnlyList<DeviceProfile> Profiles { get; }
        /// <summary>Gets the catalogs.</summary>
        public IReadOnlyList<CommandCatalog> Catalogs { get; }
        /// <summary>Gets the commands.</summary>
        public IReadOnlyList<DcsCommandDefinition> Commands { get; }
        /// <summary>Gets the bindings.</summary>
        public IReadOnlyList<BindingRelationship> Bindings { get; }
        /// <summary>Gets the UI Layer usage.</summary>
        public IReadOnlyList<UiLayerModuleUsage> UiLayerUsage { get; }
        /// <summary>Gets the UI Layer overlays.</summary>
        public IReadOnlyList<UiLayerOverlay> UiLayerOverlays { get; }
        /// <summary>Gets the UI Layer tombstones.</summary>
        public IReadOnlyList<UiLayerTombstone> UiLayerTombstones { get; }
        /// <summary>Gets the categories.</summary>
        public IReadOnlyList<KneeboardCategory> Categories { get; }
        /// <summary>Gets the pages.</summary>
        public IReadOnlyList<KneeboardPage> Pages { get; }
        /// <summary>Gets the documentation.</summary>
        public IReadOnlyList<DocumentationMetadata> Documentation { get; }
        /// <summary>Gets the packages.</summary>
        public IReadOnlyList<PackagingMetadata> Packages { get; }
        /// <summary>Gets the provenance.</summary>
        public IReadOnlyList<SourceProvenance> Provenance { get; }
        /// <summary>Gets the snapshot.</summary>
        public SnapshotIdentity Snapshot { get; }
        /// <summary>Gets the release.</summary>
        public ReleaseVersion Release { get; }

        #endregion END Public
        #region BEGIN private helpers
        private void Validate()
        {
            Unique(Modules.Select(x => x.Id), "module ID");
            Unique(Modules.Select(x => x.DcsId), "DCS input module ID", StringComparer.OrdinalIgnoreCase);
            Unique(Devices.Select(x => x.Id), "device ID");
            Unique(Devices.Select(x => x.StableKey), "device stable key", StringComparer.OrdinalIgnoreCase);
            Unique(Controls.Select(x => x.Id), "control ID");
            Unique(Controls.Select(x => $"{x.DeviceId}\0{x.StableKey}"), "control stable key",
                   StringComparer.OrdinalIgnoreCase);
            Unique(Instances.Select(x => x.Id), "device instance ID");
            Unique(Instances.Select(x => $"{x.ModuleId}\0{x.StableKey}"), "module device instance",
                   StringComparer.OrdinalIgnoreCase);
            Unique(Profiles.Select(x => x.Id), "profile ID");
            Unique(Catalogs.Select(x => x.Id), "catalog ID");
            Unique(Commands.Select(x => x.Id), "command ID");
            Unique(Bindings.Select(x => x.Id), "binding ID");
            Unique(Categories.Select(x => x.Id), "kneeboard category ID");
            Unique(Pages.Select(x => x.Id), "kneeboard page ID");
            var moduleIds = Modules.Select(x => x.Id).ToHashSet();
            var deviceIds = Devices.Select(x => x.Id).ToHashSet();
            var controlIds = Controls.Select(x => x.Id).ToHashSet();
            var profileIds = Profiles.Select(x => x.Id).ToHashSet();
            var catalogIds = Catalogs.Select(x => x.Id).ToHashSet();
            var commandIds = Commands.Select(x => x.Id).ToHashSet();
            var instanceIds = Instances.Select(x => x.Id).ToHashSet();
            var categoryIds = Categories.Select(x => x.Id).ToHashSet();
            Require(Controls, x => deviceIds.Contains(x.DeviceId), "control device");
            Require(Instances, x => moduleIds.Contains(x.ModuleId) && deviceIds.Contains(x.DeviceId),
                    "device instance");
            Require(Commands, x => catalogIds.Contains(x.CatalogId), "command catalog");
            Require(Bindings,
                    x => profileIds.Contains(x.ProfileId) && controlIds.Contains(x.ControlId) &&
                         commandIds.Contains(x.CommandId),
                    "binding");
            Require(Bindings.Where(x => x.AxisTuning is not null),
                    x => Commands.Single(c => c.Id == x.CommandId).IsAxis, "axis tuning command");
            Require(UiLayerUsage, x => moduleIds.Contains(x.ModuleId) && commandIds.Contains(x.CommandId),
                    "UI Layer utilization");
            Require(UiLayerOverlays,
                    x => moduleIds.Contains(x.ModuleId) && deviceIds.Contains(x.DeviceId) &&
                         controlIds.Contains(x.ControlId) &&
                         (x.CommandId is null || commandIds.Contains(x.CommandId.Value)),
                    "UI Layer overlay");
            Require(UiLayerTombstones, x => moduleIds.Contains(x.ModuleId), "UI Layer tombstone");
            Require(Pages,
                    x => moduleIds.Contains(x.ModuleId) &&
                         (x.DeviceInstanceId is null || instanceIds.Contains(x.DeviceInstanceId.Value)) &&
                         x.CategoryIds.All(categoryIds.Contains),
                    "kneeboard page");
            Require(Documentation, x => moduleIds.Contains(x.ModuleId), "documentation metadata");
            Require(Packages, x => moduleIds.Contains(x.ModuleId), "packaging metadata");
            if (Snapshot.SchemaVersion != CurrentSnapshotVersion)
                throw new ArgumentException("Unsupported snapshot schema version.", nameof(Snapshot));
        }
        private static T[] Required<T>(IEnumerable<T>? values, string name) => values?.ToArray() ??
                                                                               throw new ArgumentNullException(name);
        private static void Unique<T>(IEnumerable<T> values, string label, IEqualityComparer<T>? comparer = null)
        {
            var all = values.ToArray();
            if (all.Distinct(comparer).Count() != all.Length)
                throw new ArgumentException($"Duplicate {label}.");
        }
        private static void Require<T>(IEnumerable<T> values, Func<T, bool> predicate, string label)
        {
            if (values.Any(x => !predicate(x)))
                throw new ArgumentException($"Invalid {label} relationship.");
        }
        #endregion END private helpers
    }
}
