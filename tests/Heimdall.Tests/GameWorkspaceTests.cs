using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.EliteDangerous;
using Heimdall.Infrastructure;
using Heimdall.TestSupport;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Exercises game-owned authoring, isolated persistence, edition evidence and portable recovery.</summary>
    public sealed class GameWorkspaceTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "ipia-game-" + Guid.NewGuid().ToString("N"));
        #region BEGIN Public
        /// <summary>Authoring, store/reopen and native export retain both slots and independent source evidence.</summary>
        [Fact]
        public void Edit_save_reopen_export_and_discard_preserve_native_semantics()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var backend = new EliteDangerousProfileBackend();
            var session = new GameProfileSession(backend, Workspace(), issuer.Policy, [], () => []);
            var before = session.Snapshot;
            var stick = before.Profile.Devices.First(x => x.Buttons.Count > 0);
            var command = session.Catalog.Commands.First(x => x.Type == CommandType.Button);
            var axisCommand = session.Catalog.Commands.First(x => x.Type == CommandType.Axis);
            var changes = 0; session.Changed += (_, _) => changes++;
            Assert.False(session.HasChanges); Assert.Empty(session.AccessMessage);
            session.AssignButton(stick.LogicalId, command.Id, "Secondary", "Joy_7", BindingMode.Hold, []);
            session.AssignAxis(stick.LogicalId, axisCommand.Id, "Joy_XAxis", false, 12.5f);
            var toggleId = session.Snapshot.Profile.Metadata.Keys.Single()["ed.toggle:".Length..];
            session.SetToggle(toggleId, true);
            Assert.True(session.HasChanges); Assert.Equal(3, changes);
            var store = new FileGameWorkspaceStore(_root);
            var revision = store.Save(session.Snapshot, null); session.AcceptSaved();
            Assert.False(session.HasChanges);
            var reopened = new GameProfileSession(backend, store.Load(before.Id).Workspace, issuer.Policy, [], () => []);
            var native = backend.Import("controls", reopened.Export().Files);
            Assert.Contains(native.Devices.SelectMany(x => x.Buttons), x => x.SourceButton == "Joy_7" && x.Mode == BindingMode.Hold);
            Assert.Contains(native.Devices.SelectMany(x => x.Axes), x => x.Deadzone == 12.5f && !x.Invert);
            Assert.Contains(native.Metadata.Values, x => x == "1");
            reopened.Clear(command.Id, "Secondary"); Assert.True(reopened.HasChanges); reopened.Discard(); Assert.False(reopened.HasChanges);
            Assert.Equal(revision, store.Load(before.Id).Revision);
            var external = reopened.Snapshot; external.Profile.Devices.Clear();
            Assert.NotEmpty(reopened.Snapshot.Profile.Devices);
        }

        /// <summary>Failed conversions never publish partial changes or destroy the saved baseline.</summary>
        [Theory]
        [InlineData("mode")]
        [InlineData("axis")]
        [InlineData("command")]
        [InlineData("device")]
        [InlineData("slot")]
        [InlineData("toggle")]
        [InlineData("modifier")]
        public void Invalid_edits_leave_the_session_unchanged(string invalid)
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var session = new GameProfileSession(new EliteDangerousProfileBackend(), Workspace(), issuer.Policy, [], () => []);
            var snapshot = JsonSerializer.Serialize(session.Snapshot);
            var device = session.Snapshot.Profile.Devices[0];
            var command = session.Catalog.Commands.First(x => x.Type == CommandType.Button).Id;
            var axis = session.Catalog.Commands.First(x => x.Type == CommandType.Axis).Id;
            Assert.ThrowsAny<Exception>(() =>
            {
                switch (invalid)
                {
                    case "mode": session.AssignButton(device.LogicalId, command, "Primary", "Joy_7", BindingMode.Toggle, []); break;
                    case "axis": session.AssignAxis(device.LogicalId, axis, "Joy_XAxis", true, 101); break;
                    case "command": session.AssignButton(device.LogicalId, "missing", "Primary", "Joy_7", BindingMode.Press, []); break;
                    case "device": session.AssignAxis("missing", axis, "Joy_XAxis", true, 0); break;
                    case "slot": session.AssignButton(device.LogicalId, command, "Binding", "Joy_7", BindingMode.Press, []); break;
                    case "toggle": session.SetToggle("missing", true); break;
                    case "modifier": session.AssignButton(device.LogicalId, command, "Primary", "Joy_7", BindingMode.Press, [new() { Source = "missing", Value = "Joy_2" }]); break;
                }
            });
            Assert.Equal(snapshot, JsonSerializer.Serialize(session.Snapshot)); Assert.False(session.HasChanges);
        }

        /// <summary>Free requires reviewed drawings and native manufacturer evidence for every physical reference.</summary>
        [Fact]
        public void Free_edition_cannot_be_unlocked_by_game_names_or_product_tokens()
        {
            var workspace = Workspace(numeric: true);
            var physical = workspace.Profile.Devices.Where(x => !GameWorkspaceAccess.IsSystemDevice(x.Identity!.SimSpecificIds["ed.device"])).ToArray();
            var devices = physical.Select((x, i) => new DetectedDcsDevice(Guid.NewGuid(), "User-editable name " + i, true,
                ProductGuid: Guid.Parse((0x1001 + i).ToString("X4", System.Globalization.CultureInfo.InvariantCulture) + "044f-0000-0000-0000-504944564944"))).ToArray();
            var catalog = new[] { "tm-mfd", "tm-warthog-throttle" };
            var session = new GameProfileSession(new EliteDangerousProfileBackend(), workspace, new(), catalog, () => devices, new EliteDeviceReferenceMatcher());
            Assert.NotEmpty(session.AccessMessage); Assert.Throws<InvalidOperationException>(() => session.Export());
            var command = session.Catalog.Commands.First(x => x.Type == CommandType.Button).Id;
            Assert.Throws<InvalidOperationException>(() => session.AssignButton(physical[0].LogicalId, command, "Primary", "Joy_8", BindingMode.Press, []));
            Assert.Throws<InvalidOperationException>(() => session.Associate(physical[0].LogicalId, "moza-ab9", devices[0].PhysicalGuid));
            Assert.Throws<InvalidOperationException>(() => session.Associate("unknown", "tm-mfd", devices[0].PhysicalGuid));
            Assert.Throws<InvalidOperationException>(() => session.Associate(physical[0].LogicalId, "tm-mfd", Guid.NewGuid()));
            for (var i = 0; i < physical.Length; i++) session.Associate(physical[i].LogicalId, catalog[i], devices[i].PhysicalGuid);
            Assert.Empty(session.AccessMessage); Assert.True(session.Export().Succeeded);
            var offline = new GameProfileSession(new EliteDangerousProfileBackend(), session.Snapshot, new(), catalog, () => [], new EliteDeviceReferenceMatcher());
            Assert.Empty(offline.AccessMessage);
            var otherBrand = devices[0] with { ProductGuid = Guid.Parse("0001046d-0000-0000-0000-504944564944") };
            var blocked = new GameProfileSession(new EliteDangerousProfileBackend(), session.Snapshot, new(), catalog, () => [otherBrand], new EliteDeviceReferenceMatcher());
            Assert.NotEmpty(blocked.AccessMessage); Assert.Throws<InvalidOperationException>(() => blocked.Export());
            Assert.Throws<InvalidOperationException>(() => blocked.Associate(physical[0].LogicalId, "tm-mfd", otherBrand.PhysicalGuid));
            var ambiguous = new GameProfileSession(new EliteDangerousProfileBackend(), session.Snapshot, new(), catalog, () => [devices[0], devices[0]], new EliteDeviceReferenceMatcher());
            Assert.NotEmpty(ambiguous.AccessMessage);
        }

        /// <summary>Another native vendor cannot gain eligibility through an unrelated Thrustmaster drawing association.</summary>
        [Fact]
        public void Association_cannot_retarget_a_native_controller_or_grant_eligibility()
        {
            var backend = new EliteDangerousProfileBackend();
            var profile = backend.Import("controls", new Dictionary<string, string>
            { ["Synthetic.4.2.binds"] = "<Root PresetName='Synthetic' MajorVersion='4' MinorVersion='2'><PrimaryFire><Primary Device='06A30BAC' Key='Joy_1'/><Secondary Device='{NoDevice}' Key=''/></PrimaryFire></Root>" });
            var observed = new DetectedDcsDevice(Guid.NewGuid(), "Thrustmaster-looking name", true,
                ProductGuid: Guid.Parse("1001044f-0000-0000-0000-504944564944"));
            var session = new GameProfileSession(backend, new(1, Guid.NewGuid(), "Other native target", profile, new()), new(), ["tm-mfd"], () => [observed], new EliteDeviceReferenceMatcher());
            Assert.Throws<InvalidOperationException>(() => session.Associate(profile.Devices[0].LogicalId, "tm-mfd", observed.PhysicalGuid));
            Assert.Empty(session.Snapshot.Hardware);
            Assert.Throws<InvalidOperationException>(() => session.Export());
            var forged = session.Snapshot;
            forged.Hardware[profile.Devices[0].LogicalId] = new("tm-mfd", observed);
            var reopened = new GameProfileSession(backend, forged, new(), ["tm-mfd"], () => [observed], new EliteDeviceReferenceMatcher());
            Assert.NotEmpty(reopened.AccessMessage);
            Assert.Throws<InvalidOperationException>(() => reopened.Export());
        }

        /// <summary>Store copies survive serialization and stale saves fail before replacing bytes.</summary>
        [Fact]
        public void Persistence_is_independent_and_uses_revision_checks()
        {
            var store = new FileGameWorkspaceStore(_root); var workspace = Workspace();
            Assert.Empty(store.List()); Assert.Equal("dcs", store.ReadSelection().BackendId);
            var revision = store.Save(workspace, null); Assert.Single(store.List());
            Assert.Throws<InvalidOperationException>(() => store.Save(workspace, null));
            var loaded = store.Load(workspace.Id); loaded.Workspace.Profile.Devices.Clear();
            Assert.NotEmpty(store.Load(workspace.Id).Workspace.Profile.Devices);
            var changed = workspace with { Name = "My New Controls" };
            var next = store.Save(changed, revision); Assert.NotEqual(revision, next);
            Assert.Throws<InvalidOperationException>(() => store.Save(workspace, revision));
            Assert.Equal("My New Controls", store.Load(workspace.Id).Workspace.Name);
            store.SaveSelection(new("elite-dangerous", workspace.Id, DisplayName: changed.Name));
            Assert.Equal(workspace.Id, new FileGameWorkspaceStore(_root).ReadSelection().WorkspaceId);
            Assert.Equal("elite-dangerous", Assert.Single(store.List()).BackendId);
            Assert.Throws<ArgumentException>(() => store.Load(Guid.Empty));
        }

        /// <summary>Malformed saved documents remain on disk and are reported rather than silently replaced.</summary>
        [Theory]
        [InlineData("json")]
        [InlineData("null")]
        [InlineData("schema")]
        [InlineData("backend")]
        [InlineData("identity")]
        [InlineData("unknown")]
        public void Invalid_saved_envelopes_are_rejected(string failure)
        {
            var store = new FileGameWorkspaceStore(_root); var workspace = Workspace(); store.Save(workspace, null);
            var path = Path.Combine(_root, "GameWorkspaces", workspace.Id.ToString("N") + ".ipia-game.json");
            var text = File.ReadAllText(path);
            text = failure switch
            {
                "json" => "{", "null" => "null", "schema" => text.Replace("\"SchemaVersion\":1", "\"SchemaVersion\":9", StringComparison.Ordinal),
                "backend" => text.Replace("elite-dangerous", "other-game", StringComparison.Ordinal),
                "identity" => text.Replace(workspace.Id.ToString("D"), Guid.NewGuid().ToString("D"), StringComparison.Ordinal),
                _ => text.Insert(1, "\"Future\":true,")
            };
            File.WriteAllText(path, text);
            Assert.Throws<InvalidDataException>(() => store.Load(workspace.Id));
            Assert.Equal(text, File.ReadAllText(path));
        }

        /// <summary>Selections cannot smuggle unsupported games or invalid identities into startup.</summary>
        [Theory]
        [InlineData("backend")]
        [InlineData("id")]
        [InlineData("name")]
        public void Invalid_recent_selections_are_rejected(string failure)
        {
            var store = new FileGameWorkspaceStore(_root);
            var selection = failure switch
            {
                "backend" => new GameSelectionState("future"),
                "id" => new GameSelectionState(WorkspaceId: Guid.Empty),
                _ => new GameSelectionState(DisplayName: new string('x', 201))
            };
            Assert.Throws<InvalidDataException>(() => store.SaveSelection(selection));
            Assert.Equal(new GameSelectionState(), store.ReadSelection());
        }

        /// <summary>The existing full-profile backup includes game workspaces and recent selection without a database migration.</summary>
        [Fact]
        public async Task Full_profile_backup_restores_game_documents()
        {
            var source = Path.Combine(_root, "source"); var target = Path.Combine(_root, "target");
            Directory.CreateDirectory(source); Directory.CreateDirectory(target);
            await new SqliteMigrationRunner(Connection(source)).MigrateAsync();
            await new SqliteMigrationRunner(Connection(target)).MigrateAsync();
            var workspace = Workspace(); var store = new FileGameWorkspaceStore(source); store.Save(workspace, null);
            store.SaveSelection(new("elite-dangerous", workspace.Id, DisplayName: workspace.Name));
            var archive = Path.Combine(_root, "games.zip"); new UserDataRecoveryService(source, "1.0.0.0").Backup(archive);
            new UserDataRecoveryService(target, "1.0.0.0").Restore(archive);
            var restored = new FileGameWorkspaceStore(target);
            Assert.Equal(JsonSerializer.Serialize(workspace), JsonSerializer.Serialize(restored.Load(workspace.Id).Workspace));
            Assert.Equal(workspace.Id, restored.ReadSelection().WorkspaceId);
        }

        /// <summary>Review exports use new folders, never overwrite prior results and refuse active destinations.</summary>
        [Fact]
        public void Review_export_is_complete_isolated_and_cancellable()
        {
            Directory.CreateDirectory(_root);
            var active = Path.Combine(_root, "active", "Bindings");
            var files = new EliteDangerousProfileBackend().Export(Workspace().Profile).Files;
            Assert.Throws<InvalidOperationException>(() => GameReviewExport.Write(active, files, [active]));
            Assert.Throws<InvalidOperationException>(() => GameReviewExport.Write(Path.Combine(active, "child"), files, [active]));
            var output = GameReviewExport.Write(_root, files, [active]);
            Assert.Equal(files["Synthetic.4.2.binds"], File.ReadAllText(Path.Combine(output, "Synthetic.4.2.binds")));
            Assert.NotEqual(output, GameReviewExport.Write(_root, files, [active]));
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            Assert.Throws<OperationCanceledException>(() => GameReviewExport.Write(_root, files, [active], cancelled.Token));
            Assert.Empty(Directory.GetDirectories(_root, ".*.tmp"));
        }

        /// <summary>Review writers independently reject unsafe or incomplete file plans.</summary>
        [Theory]
        [InlineData("../escape.binds")]
        [InlineData("folder/file.binds")]
        [InlineData("C:escape.binds")]
        [InlineData("bad.txt")]
        public void Unsafe_review_plans_are_rejected(string name)
        {
            Assert.Throws<InvalidDataException>(() => GameReviewExport.Write(_root, new Dictionary<string, string> { [name] = "text" }, []));
            Assert.False(Directory.Exists(_root));
        }
        /// <summary>Releases temporary profile data.</summary>
        public void Dispose() { SqliteConnection.ClearAllPools(); if (Directory.Exists(_root)) Directory.Delete(_root, true); }
        #endregion END Public
        #region BEGIN private helpers
        private static string Connection(string root) => new SqliteConnectionStringBuilder { DataSource = Path.Combine(root, "ipia.db"), Pooling = false }.ToString();
        private static GameWorkspace Workspace(bool numeric = false)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "EliteDangerous", "Synthetic.4.2.binds");
            var text = File.ReadAllText(path);
            if (numeric) text = text.Replace("ExampleStick", "044F1001", StringComparison.Ordinal).Replace("ExampleThrottle", "044F1002", StringComparison.Ordinal);
            var profile = new EliteDangerousProfileBackend().Import("controls", new Dictionary<string, string> { ["Synthetic.4.2.binds"] = text });
            return new(1, Guid.NewGuid(), "My Controls", profile, new());
        }
        #endregion END private helpers
    }
}
