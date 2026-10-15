using System.IO;
using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DcsIpia.Presentation
{
    /// <summary>Owns one disposable training database, sample source tree and confined output directory.</summary>
    public sealed class TrainingEnvironment : IDisposable
    {
        private readonly string _connection;
        /// <summary>Allocates a fresh unpredictable training directory; never accepts a user database or DCS path.</summary>
        public TrainingEnvironment()
        {
            Root = Path.Combine(Path.GetTempPath(), "DCS-IPIA-Training", Guid.NewGuid().ToString("N"));
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
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
        #endregion END Public
    }
}
