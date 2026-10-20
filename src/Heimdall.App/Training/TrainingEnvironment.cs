using System.IO;
using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Heimdall.Dcs;
using Heimdall.Infrastructure;
using Microsoft.Data.Sqlite;

namespace Heimdall.App
{
    /// <summary>Owns one disposable training database, sample source tree and confined output directory.</summary>
    public sealed class TrainingEnvironment : IDisposable
    {
        private readonly string _connection;
        private bool _disposed;
        /// <summary>Allocates a fresh unpredictable training directory; never accepts a user database or DCS path.</summary>
        public TrainingEnvironment()
        {
            Root = Path.Combine(Path.GetTempPath(), "HEIMDALL-Training", Guid.NewGuid().ToString("N"));
            _connection = new SqliteConnectionStringBuilder { DataSource = Path.Combine(Root, "ipia.db"), ForeignKeys = true, Pooling = false }.ToString();
            Repository = new SqliteEditorWorkspaceRepository(_connection);
        }
        #region BEGIN Public
        /// <summary>Gets the owned temporary directory.</summary>
        public string Root { get; }
        /// <summary>Gets the training-only repository.</summary>
        public SqliteEditorWorkspaceRepository Repository { get; }
        /// <summary>Gets the supplied import folder.</summary>
        public string ImportFolder => Path.Combine(Root, "Saved Games", "DCS", "Config", "Input", "TRAINING");
        /// <summary>Gets the training output directory.</summary>
        public string Outputs => Path.Combine(Root, "Outputs");
        /// <summary>Gets the saved, fictional UI baseline.</summary>
        public SharedUiLayerWorkspace Defaults { get; private set; } = null!;
        /// <summary>Gets pinned shipped hardware sources.</summary>
        public IReadOnlyDictionary<string, string> Hardware { get; private set; } = null!;
        /// <summary>Gets whether this initialized session still owns its temporary resources.</summary>
        internal bool IsActive => !_disposed && Hardware is not null;
        /// <summary>Gets the scripted source used by this environment's input-test demonstration.</summary>
        internal TrainingInputSource? Input { get; private set; }
        /// <summary>Rejects attaching training access to another repository or an expired session.</summary>
        internal void RequireRepository(IEditorWorkspaceRepository? repository)
        {
            if (_disposed || Hardware is null || !ReferenceEquals(repository, Repository))
                throw new InvalidOperationException("Training access requires this active session's isolated repository.");
        }
        /// <summary>Requires this environment's own fixture catalog and default revision for creation/import.</summary>
        internal void RequireSources(IEditorWorkspaceRepository repository, SharedUiLayerWorkspace defaults, IReadOnlyDictionary<string, string> hardware)
        {
            RequireRepository(repository);
            if (!ReferenceEquals(defaults, Defaults) || !ReferenceEquals(hardware, Hardware))
                throw new InvalidOperationException("Training forms require the session's own sample sources.");
        }
        /// <summary>Permits only the fictional aircraft and physical profiles inside this session.</summary>
        internal void RequireWorkspace(EditorWorkspaceSnapshot workspace)
        {
            RequireRepository(Repository);
            if (workspace.Configuration.Module.DcsId != "TRAINING" || workspace.Configuration.Profiles.Any(profile =>
                profile.DeviceId != "training-joystick" || profile.SourceName != TrainingDiscovery.Unit.FileName))
                throw new InvalidOperationException("Training access is limited to the fictional sample aircraft and controller.");
        }
        /// <summary>Creates simulated input only for the fictional identity, never a native device.</summary>
        internal IDeviceInputSource CreateInputSource(nint window, IReadOnlyList<Guid> devices)
        {
            RequireRepository(Repository);
            if (devices.Count != 1 || devices[0] != TrainingDiscovery.Unit.PhysicalGuid)
                throw new InvalidOperationException("Training cannot acquire physical controllers.");
            Input?.Dispose();
            return Input = new TrainingInputSource();
        }
        /// <summary>Creates SQLite and sample sources without reading any user configuration.</summary>
        public async Task InitializeAsync()
        {
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(Outputs);
            Directory.CreateDirectory(Path.Combine(ImportFolder, "joystick"));
            await new SqliteMigrationRunner(_connection).MigrateAsync();
            var sample = JsonSerializer.Deserialize<WorkspaceHardware>(TrainingAssets.Read("joystick.json"))!;
            sample = sample with { Diagram = sample.Diagram with { Svg = TrainingAssets.Read("joystick.svg") } };
            Hardware = new Dictionary<string, string> { ["config/ipia-hardware.json"] = JsonSerializer.Serialize(new[] { sample }),
                [sample.Diagram.Path] = sample.Diagram.Svg };
            await File.WriteAllTextAsync(Path.Combine(ImportFolder, "joystick", TrainingDiscovery.Unit.FileName), TrainingAssets.Read("profile.lua"));
            await File.WriteAllTextAsync(Path.Combine(ImportFolder, "modifiers.lua"), TrainingAssets.Read("modifiers.lua"));
            await File.WriteAllTextAsync(Path.Combine(Root, "commands.json"), TrainingAssets.Read("commands.json"));
            var path = "assets/shared/ui-layer/input/UiLayer/joystick/" + TrainingDiscovery.Unit.FileName;
            var sources = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["assets/shared/ui-layer/functions.json"] = TrainingAssets.Read("ui-commands.json"),
                ["assets/shared/ui-layer/input/UiLayer/modifiers.lua"] = "local modifiers = {} return modifiers",
                [path] = "local diff = {} return diff",
                ["assets/shared/ui-layer/ipia-device-hardware.json"] = JsonSerializer.Serialize(new Dictionary<string, string> { [path] = "training-joystick" })
            };
            Defaults = await new SqliteSharedUiLayerWorkspaceRepository(_connection).SaveAsync(new(0, BundledSharedCatalog.SourceCommit(), sources));
        }
        /// <summary>Deletes only this environment after its runner and views have settled.</summary>
        public void Dispose()
        {
            _disposed = true;
            Input?.Dispose();
            Input = null;
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
        #endregion END Public
    }
}
