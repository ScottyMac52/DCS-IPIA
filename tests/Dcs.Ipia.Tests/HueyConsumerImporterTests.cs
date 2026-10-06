using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Characterizes read-only Huey imports against a pinned consumer source commit.</summary>
    public sealed class HueyConsumerImporterTests
    {
        #region BEGIN Public
        /// <summary>The real Huey base filename pins its Hornet UI Layer through import, SQLite, and Lua packaging.</summary>
        [Fact]
        public async Task ExportsHueyUiLayerDespiteSharedBaseClassification()
        {
            var request = Fixture();
            var shared = request.SharedLua!;
            var catalog = request.Catalog!;
            var imported = new ConsumerSourceImporter().Import(request);
            Assert.True(imported.Succeeded, string.Join("; ", imported.Diagnostics.Select(x => x.Message)));
            var configuration = imported.Configuration!;
            var hornet = Assert.Single(configuration.Profiles, x => x.DeviceId == "moza-ab9-hornet-grip");
            var canonical = Assert.Single(shared.Profiles, x => x.SourceName == hornet.SourceName);
            Assert.Equal("moza-ab9", canonical.DeviceId);
            Assert.Equal(canonical, ModuleUiLayerBindingImporter.ResolveCanonicalProfile(imported, shared, hornet));
            var graphics = catalog.Devices.ToDictionary(
                device => "assets/shared/hardware/svg/" + device.StableKey + ".svg",
                device => "<svg xmlns='http://www.w3.org/2000/svg' width='4000' height='4000' viewBox='0 0 4000 4000'>" +
                    "<image x='0' y='0' width='4000' height='4000' href='data:image/png;base64,AA=='/>" +
                    string.Join("", shared.Controls.Where(x => x.Control.DeviceId == device.Id).Select(x => x.Control.StableKey)
                        .Concat(catalog.Templates.Where(x => x.DeviceKey == device.StableKey).SelectMany(x => x.Bindings.Values))
                        .Distinct(StringComparer.Ordinal).Select(
                        (control, i) => $"<!-- box:{control} --><rect x='{i % 20 * 80}' y='{i / 20 * 60}' width='60' height='40'/>")) +
                    "</svg>");
            var diagrams = UiLayerDiagramFactory.Create(imported, catalog, graphics, shared);
            var hornetDiagram = Assert.Single(diagrams, x => x.ProfileId == hornet.Id);
            Assert.EndsWith("/" + hornet.SourceName, hornetDiagram.CanonicalUiLayerSourcePath!, StringComparison.Ordinal);
            Assert.Equal(3, hornetDiagram.Callouts.Count);
            var assignments = diagrams.SelectMany(diagram => diagram.Callouts.Select(callout =>
            {
                var control = diagram.Controls.Single(x => x.Control == callout.Control);
                return new DraftAssignment(new(configuration.Module.Id, diagram.ProfileId, AssignmentLayer.UiLayer,
                    BindingSection.Key, control.InputKey, callout.Modifier.Replace(" + ", "+", StringComparison.Ordinal)),
                    new(callout.CommandId, callout.Function, false, AssignmentLayer.UiLayer));
            })).ToArray();
            var luaRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DcsCommon", "Lua");
            var sources = imported.Sources.ToDictionary(x => x.Key, x => x.Value);
            foreach (var file in Directory.GetFiles(luaRoot, "*.lua", SearchOption.AllDirectories))
            {
                var path = Path.GetRelativePath(luaRoot, file).Replace('\\', '/');
                if (path.StartsWith("assets/shared/ui-layer/input/UiLayer/", StringComparison.Ordinal))
                    sources[path] = File.ReadAllText(file);
            }
            var workspace = new EditorWorkspaceSnapshot(configuration, imported.Pages, [], diagrams, [],
                catalog.Commands, assignments, KneeboardLabelImporter.Import(imported.Metadata, configuration), sources);
            var database = Path.Combine(Path.GetTempPath(), "HueyUiLayer-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var connection = "Data Source=" + database + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                await repository.SaveAsync(workspace);
                var reopened = await repository.GetAsync(configuration.Module.Id);
                Assert.NotNull(reopened);
                var output = SnapshotLuaPayloadRenderer.Generate(reopened);
                var path = "Config/Input/UiLayer/joystick/" + hornet.SourceName;
                var bindings = DcsLuaParser.ParseProfile(Encoding.UTF8.GetString(output[path]), hornet.Id);
                Assert.All(assignments.Where(x => x.Slot.ProfileId == hornet.Id), assignment =>
                    Assert.Contains(bindings, x => x.CommandId == assignment.Command.Id && x.InputKey == assignment.Slot.InputKey));
                var legacy = reopened with { Diagrams = reopened.Diagrams.Select(x => x.ProfileId == hornet.Id
                    ? x with { CanonicalUiLayerSourcePath = null } : x).ToArray() };
                Assert.Equal(output[path], SnapshotLuaPayloadRenderer.Generate(legacy)[path]);
            }
            finally
            {
                if (File.Exists(database))
                    File.Delete(database);
            }
        }
        /// <summary>Real Huey sources retain five profiles, three MFD roles, pages, UI usage, labels, and exact source hashes.</summary>
        [Fact]
        public void Imports_real_huey_sources_without_losing_authored_metadata()
        {
            var request = Fixture();
            var result = new ConsumerSourceImporter().Import(request);
            Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
            Assert.Equal(5, result.Configuration!.Profiles.Count);
            Assert.Equal(5, result.Instances.Count);
            Assert.Equal(6, result.Pages.Count);
            Assert.Equal(12, result.Sources.Count);
            Assert.Equal(6, result.Metadata.Count);
            Assert.Equal(12, result.Provenance.Count);
            Assert.Equal(101, result.Metadata["config/kneeboard.json"].GetProperty("labels").EnumerateObject().Count());
            Assert.Equal(16, result.Metadata["config/kneeboard.json"]
                                 .GetProperty("uiLayerUtilization")
                                 .GetProperty("bindings")
                                 .GetArrayLength());
            Assert.Contains(result.Instances, x => x.StableKey == "tm-mfd-3" && x.Role == "MFD3");
            Assert.Contains(result.Configuration.Bindings, x => x.Reformers.Contains("MOZA_F16_F18_BTN3"));
            Assert.Contains(result.Configuration.Bindings, x => x.Section == BindingSection.Axis);
            Assert.Contains(result.Pages, x => x.StableKey == "02-TM-MFD-2" && x.DeviceInstanceId is not null);
            Assert.Contains(result.Usage, x => x.CommandId == StableIdentity.Create("ui-function", "vr-zoom"));
            var source = request.Sources.Single(x => x.Path == "config/kneeboard.json");
            Assert.Equal(source.Content, result.Sources[source.Path]);
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source.Content))),
                         result.Provenance.Single(x => x.Path == source.Path).Sha256);
            Assert.All(result.Provenance, x =>
                                          {
                                              Assert.Equal(request.Commit, x.Commit);
                                              Assert.Equal("UH-1H", x.Repository);
                                          });
        }
        /// <summary>Documentation and release inputs are pinned without interpreting generated content as authored bindings.</summary>
        [Fact]
        public void Preserves_committed_documentation_and_release_contract_texts()
        {
            var request = Fixture();
            ConsumerSource[] references = [
                new("README.md", "# Huey\n"), new("docs/INSTALLATION.md", "Install notes\n"),
                new("docs/devices/TM-MFD-1-MAPPINGS.md", "Device mappings\n"),
                new("packaging/ovgme/README.TXT", "Package instructions\n"),
                new("scripts/Build-OvGME.ps1", "Write-Output 'build'\n"),
                new(".github/workflows/release.yml", "name: Release\n")
            ];
            var imported =
                new ConsumerSourceImporter().Import(request with { Sources = [..request.Sources, ..references] });
            Assert.True(imported.Succeeded, string.Join("; ", imported.Diagnostics.Select(x => x.Message)));
            Assert.Equal(5, imported.Configuration!.Profiles.Count);
            Assert.Equal(request.Sources.Count + references.Length, imported.Provenance.Count);
            foreach (var source in references)
            {
                Assert.Equal(source.Content, imported.Sources[source.Path]);
                Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source.Content))),
                             imported.Provenance.Single(x => x.Path == source.Path).Sha256);
                Assert.False(imported.Metadata.ContainsKey(source.Path));
            }
        }
        /// <summary>Repeat imports preserve ordering, stable IDs, and content hashes.</summary>
        [Fact]
        public void Repeated_import_is_deterministic()
        {
            var request = Fixture();
            var importer = new ConsumerSourceImporter();
            var first = importer.Import(request);
            var second = importer.Import(request);
            Assert.True(first.Succeeded);
            Assert.True(second.Succeeded);
            Assert.Equal(first.Configuration!.Profiles, second.Configuration!.Profiles);
            Assert.Equal(first.Configuration.Bindings.Select(x => (x.ProfileId, x.CommandId, x.InputKey)),
                         second.Configuration.Bindings.Select(x => (x.ProfileId, x.CommandId, x.InputKey)));
            Assert.Equal(first.Instances, second.Instances);
            Assert.Equal(first.Pages, second.Pages);
            Assert.Equal(first.Provenance, second.Provenance);
        }
        /// <summary>Machine-specific GUID changes do not change MFD profile or instance identity.</summary>
        [Fact]
        public void Machine_guid_does_not_define_mfd_identity()
        {
            var request = Fixture();
            const string oldGuid = "51FA60C0-CB32-11ed-800B-444553540000";
            const string newGuid = "11111111-1111-1111-1111-111111111111";
            var sources =
                request.Sources
                    .Select(x =>
                                x.Path.Contains(oldGuid, StringComparison.Ordinal)
                                    ? x with { Path = x.Path.Replace(oldGuid, newGuid, StringComparison.Ordinal) }
                                : x.Path == "config/kneeboard.json"
                                    ? x with { Content = x.Content.Replace(oldGuid, newGuid, StringComparison.Ordinal) }
                                    : x)
                    .ToArray();
            var changed = new ConsumerSourceImporter().Import(request with { Sources = sources });
            var original = new ConsumerSourceImporter().Import(request);
            Assert.True(changed.Succeeded, string.Join("; ", changed.Diagnostics.Select(x => x.Message)));
            Assert.Equal(original.Configuration!.Profiles.Single(x => x.Instance == "MFD1").Id,
                         changed.Configuration!.Profiles.Single(x => x.Instance == "MFD1").Id);
            Assert.Equal(original.Instances.Single(x => x.Role == "MFD1").Id,
                         changed.Instances.Single(x => x.Role == "MFD1").Id);
        }
        /// <summary>A kneeboard profile must point to a supplied Lua file.</summary>
        [Fact]
        public void Missing_profile_is_rejected_without_partial_data()
        {
            var request = Fixture();
            var sources =
                request.Sources.Where(x => !x.Path.Contains("F16 MFD 1 ", StringComparison.Ordinal)).ToArray();
            var result = new ConsumerSourceImporter().Import(request with { Sources = sources });
            Assert.False(result.Succeeded);
            Assert.Null(result.Configuration);
            Assert.Empty(result.Provenance);
            Assert.Contains(result.Diagnostics, x => x.Code == "invalid-huey-source" &&
                                                     x.Message.Contains("no supplied Lua", StringComparison.Ordinal));
        }
        /// <summary>Conflicting author overrides cannot silently change profile devices.</summary>
        [Fact]
        public void Device_override_conflict_is_reported()
        {
            var request = Fixture();
            var sources = Replace(request, "config/scaffold-device-overrides.json",
                                  x => x.Replace("\"tm-mfd\"", "\"tm-warthog-throttle\"", StringComparison.Ordinal));
            var result = new ConsumerSourceImporter().Import(request with { Sources = sources });
            Assert.False(result.Succeeded);
            Assert.Contains(result.Diagnostics, x => x.Message.Contains("Device override", StringComparison.Ordinal));
        }
        /// <summary>Page callouts must resolve to authored labels.</summary>
        [Fact]
        public void Missing_kneeboard_label_is_reported()
        {
            var request = Fixture();
            var sources = Replace(request, "config/kneeboard.json",
                                  x =>
                                  {
                                      var root = JsonNode.Parse(x)!;
                                      root["pages"]![0]!["controls"]!["mfd-osb-t3"]!["labelId"] = "missing-label";
                                      return root.ToJsonString();
                                  });
            var result = new ConsumerSourceImporter().Import(request with { Sources = sources });
            Assert.False(result.Succeeded);
            Assert.Contains(result.Diagnostics, x => x.Message.Contains("missing-label", StringComparison.Ordinal));
        }
        /// <summary>Module UI usage cannot invent shared function identities.</summary>
        [Fact]
        public void Unknown_ui_function_is_rejected()
        {
            var request = Fixture();
            var sources = Replace(request, "config/kneeboard.json",
                                  x =>
                                  {
                                      var root = JsonNode.Parse(x)!;
                                      root["uiLayerUtilization"]!["bindings"]![0]!["functionId"] = "unknown-function";
                                      return root.ToJsonString();
                                  });
            var result = new ConsumerSourceImporter().Import(request with { Sources = sources });
            Assert.False(result.Succeeded);
            Assert.Contains(result.Diagnostics,
                            x => x.Message.Contains("Unknown canonical UI Layer function", StringComparison.Ordinal));
        }
        /// <summary>Module UI usage must reference a canonical modifier.</summary>
        [Fact]
        public void Unknown_ui_modifier_is_rejected()
        {
            var request = Fixture();
            var sources = Replace(request, "config/kneeboard.json",
                                  x =>
                                  {
                                      var root = JsonNode.Parse(x)!;
                                      root["uiLayerUtilization"]!["bindings"]![0]!["modifiers"]![0] =
                                          "unknown-modifier";
                                      return root.ToJsonString();
                                  });
            var result = new ConsumerSourceImporter().Import(request with { Sources = sources });
            Assert.False(result.Succeeded);
            Assert.Contains(result.Diagnostics,
                            x => x.Message.Contains("Unknown canonical UI Layer modifier", StringComparison.Ordinal));
        }
        /// <summary>Unsupported overlay assignments are diagnosed until their shape has an importer.</summary>
        [Fact]
        public void Unmapped_overlay_assignments_are_rejected()
        {
            var request = Fixture();
            var sources =
                Replace(request, "config/ui-layer-overlay.json",
                        x => x.Replace("\"assignments\": []", "\"assignments\": [{}]", StringComparison.Ordinal));
            var result = new ConsumerSourceImporter().Import(request with { Sources = sources });
            Assert.True(result.Succeeded);
            Assert.Contains(result.Diagnostics, x => x.Code == "module-overlay-preserved");
        }
        /// <summary>An active chord must use a modifier defined by the module itself.</summary>
        [Fact]
        public void Undefined_active_modifier_rejects_the_whole_import()
        {
            var request = Fixture();
            var sources = request.Sources
                              .Select(source => source.Path.Contains("F16 MFD 2 ", StringComparison.Ordinal)
                                                    ? source with { Content = source.Content.Replace(
                                                                        "MOZA_F16_F18_BTN3", "UNKNOWN_CHORD",
                                                                        StringComparison.Ordinal) }
                                                    : source)
                              .ToArray();
            var result = new ConsumerSourceImporter().Import(request with { Sources = sources });
            Assert.False(result.Succeeded);
            Assert.Null(result.Configuration);
            Assert.Empty(result.Provenance);
            Assert.Contains(result.Diagnostics,
                            x => x.Code == "invalid-huey-source" &&
                                 x.Message.Contains("undefined modifiers: UNKNOWN_CHORD", StringComparison.Ordinal));
        }
        /// <summary>Two page identities must not collapse onto one physical device instance.</summary>
        [Fact]
        public void Duplicate_device_instance_rejects_the_whole_import()
        {
            var request = Fixture();
            var sources = Replace(request, "config/kneeboard.json",
                                  text =>
                                  {
                                      var root = JsonNode.Parse(text)!;
                                      root["pages"]![1]!["deviceInstance"] = "MFD1";
                                      return root.ToJsonString();
                                  })
                              .Where(x => x.Path != "config/scaffold-instance-roles.json")
                              .ToArray();
            var result = new ConsumerSourceImporter().Import(request with { Sources = sources });
            Assert.False(result.Succeeded);
            Assert.Null(result.Configuration);
            Assert.Empty(result.Provenance);
            Assert.Contains(result.Diagnostics,
                            x => x.Code == "invalid-huey-source" &&
                                 x.Message.Contains("same device instance", StringComparison.Ordinal));
        }
        /// <summary>Malformed authored JSON yields a diagnostic and no partial configuration.</summary>
        [Fact]
        public void Malformed_json_is_atomic()
        {
            var request = Fixture();
            var sources = Replace(request, "config/kneeboard.json",
                                  _ => "{");
            var result = new ConsumerSourceImporter().Import(request with { Sources = sources });
            Assert.False(result.Succeeded);
            Assert.Empty(result.Metadata);
            Assert.Empty(result.Sources);
            Assert.Contains(result.Diagnostics, x => x.Code == "invalid-json");
        }
        /// <summary>Repository metadata must retain safe paths and an exact commit.</summary>
        [Fact]
        public void Invalid_provenance_is_rejected()
        {
            var request = Fixture() with { Commit = "main", Sources = [new("../bad.json", "{}")] };
            var result = new ConsumerSourceImporter().Import(request);
            Assert.False(result.Succeeded);
            Assert.Contains(result.Diagnostics, x => x.Code == "invalid-commit");
        }
        /// <summary>Two same-model devices with null page instances remain distinct and keep their own labels.</summary>
        [Fact]
        public void Repeated_device_without_instance_uses_authored_profile_keys()
        {
            var request = Fixture();
            var sources = request.Sources.Where(x => x.Path != "config/scaffold-instance-roles.json")
                              .Select(source =>
                                      {
                                          if (source.Path != "config/kneeboard.json")
                                              return source;
                                          var root = JsonNode.Parse(source.Content)!;
                                          root["pages"]![0]!["deviceInstance"] = null;
                                          root["pages"]![1]!["deviceInstance"] = null;
                                          return source with { Content = root.ToJsonString() };
                                      })
                              .ToArray();
            var imported = new ConsumerSourceImporter().Import(request with { Sources = sources });
            Assert.True(imported.Succeeded, string.Join("; ", imported.Diagnostics.Select(x => x.Message)));
            var first = imported.Configuration!.Profiles.Single(x => x.Instance == "tm-mfd-1");
            var second = imported.Configuration.Profiles.Single(x => x.Instance == "tm-mfd-2");
            Assert.NotEqual(first.Id, second.Id);
            Assert.Contains(imported.Configuration.Bindings, x => x.ProfileId == first.Id);
            Assert.Contains(imported.Configuration.Bindings, x => x.ProfileId == second.Id);
            var labels = KneeboardLabelImporter.Import(imported.Metadata, imported.Configuration);
            Assert.Equal(101, labels.Count);
            Assert.Contains(labels.Keys, x => x.Split('\u001f')[1] == first.Id.ToString());
            Assert.Contains(labels.Keys, x => x.Split('\u001f')[1] == second.Id.ToString());
            Assert.Equal(2, imported.Instances.Count(x => x.StableKey is "tm-mfd-1" or "tm-mfd-2"));
        }
        /// <summary>Imports the committed F4U-1D module with both quadrants and obsolete removed chords.</summary>
        [Fact]
        public void Imports_real_f4u_sources_without_conflating_quadrants()
        {
            var common = Fixture();
            var root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "F4U");
            var sources = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
                              .Select(x => new ConsumerSource(Path.GetRelativePath(root, x).Replace('\\', '/'),
                                                              File.ReadAllText(x)))
                              .OrderBy(x => x.Path, StringComparer.Ordinal)
                              .ToArray();
            var request = common with { Commit = "4cd755aa9dc36384e75d054893213b8a77bc26dc",
                                        Repository = "ScottyMac52/DCS-F4U-1D-Components", Sources = sources };
            var imported = new ConsumerSourceImporter().Import(request);
            Assert.True(imported.Succeeded, string.Join("; ", imported.Diagnostics.Select(x => x.Message)));
            Assert.Equal("F4U-1D", imported.Configuration!.Module.DcsId);
            Assert.Equal(10, imported.Configuration.Profiles.Count);
            var quadrants =
                imported.Configuration.Profiles.Where(x => x.DeviceId == "logitech-throttle-quadrant").ToArray();
            Assert.Equal(2, quadrants.Length);
            Assert.NotEqual(quadrants[0].Id, quadrants[1].Id);
            Assert.All(quadrants, x => Assert.Contains(imported.Configuration.Bindings, y => y.ProfileId == x.Id));
            Assert.Contains(imported.Configuration.Bindings,
                            x => x.Disposition == BindingDisposition.Removed && x.Reformers.Contains("JOY_BTN3"));
            var labels = KneeboardLabelImporter.Import(imported.Metadata, imported.Configuration);
            Assert.All(quadrants, x => Assert.Contains(labels.Keys, key => key.Split('\u001f')[1] == x.Id.ToString()));
            Assert.Contains(imported.Diagnostics, x => x.Code == "module-overlay-preserved");
            var tinyImage = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/ZQAAAABJRU5ErkJggg==");
            var graphics = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var device in common.Catalog!.Devices.Where(
                         x => imported.Configuration.Profiles.Any(y => y.DeviceId == x.StableKey)))
            {
                var controls = common.SharedLua!.Controls.Where(x => x.Control.DeviceId == device.Id)
                                   .Select(x => x.Control.StableKey)
                                   .ToArray();
                Assert.NotEmpty(controls);
                var calloutControls =
                    device.StableKey == "tm-mfd" ? controls.Concat(["mfd-osb-b1-shifted"]) : controls.AsEnumerable();
                var callouts = string.Join(
                    "\n",
                    calloutControls.Select(
                        (control, index) =>
                            $"<!-- callout:{control} -->{(device.StableKey=="logitech-throttle-quadrant"&&index==0?"<polyline/>":"<line/>")}<circle/><rect x='10' y='{10+index*30}' width='100' height='24'/><text id='lbl-{control}'>{control}</text>"));
                graphics.Add(
                    $"assets/shared/hardware/svg/{device.StableKey}.svg",
                    $"<svg xmlns='http://www.w3.org/2000/svg' width='960' height='2000' viewBox='0 0 960 2000'><image href='data:image/png;base64,{Convert.ToBase64String(tinyImage)}' x='0' y='0' width='960' height='2000'/>{callouts}</svg>");
            }
            var diagrams = UiLayerDiagramFactory.Create(imported, common.Catalog, graphics, common.SharedLua);
            Assert.Equal(imported.Configuration.Profiles.Count, diagrams.Count);
            Assert.All(diagrams, x => Assert.NotEmpty(x.Controls));
            var stick = imported.Configuration.Profiles.Single(x => x.DeviceId == "vkb-f14-gunfighter");
            var stickDiagram = diagrams.Single(x => x.ProfileId == stick.Id);
            Assert.Contains(stickDiagram.Controls,
                            x => x.InputKey == "JOY_BTN5" &&
                                 imported.Configuration.Bindings.Any(
                                     y => y.ProfileId == stick.Id && y.Disposition != BindingDisposition.Removed &&
                                          y.Reformers.Count == 0 && y.InputKey == x.InputKey));
            Assert.Equal(
                2, diagrams.Count(x => x.Path.EndsWith("/logitech-throttle-quadrant.svg", StringComparison.Ordinal)));
            Assert.Contains(stickDiagram.Callouts,
                            x => x.Modifier == "VKB_F14_BTN7" && x.CommandId == "d2604pnilu2604cdnilvd1vpnilvu0");
            Assert.Equal(7, stickDiagram.Callouts.Count);
            Assert.DoesNotContain(diagrams.SelectMany(x => x.Callouts),
                                  x => x.Modifier is "AVA_BASE_MODIFIER_BTN3" or "MOZA_MODIFIER_BTN3");
            Assert.NotEmpty(diagrams.Single(x => x.Instance == "MFD3").Callouts);
            Assert.All(diagrams.Single(x => x.Instance == "MFD3").Callouts,
                       x => Assert.Equal("VKB_F14_BTN7", x.Modifier));
            Assert.Contains(diagrams.Single(x => x.Instance == "MFD3").Callouts,
                            x => x.Control == "mfd-osb-b1-shifted" && x.Function == "Restart Mission");
        }
        /// <summary>Only kneeboard-declared profiles become module devices even when the checkout contains other Lua files.</summary>
        [Fact]
        public void Unreferenced_lua_profiles_do_not_block_module_import()
        {
            var request = Fixture();
            var sources =
                request.Sources
                    .Concat([
                        new ConsumerSource("src/Config/Input/UH-1H/joystick/Old Controller.diff.lua", "return {}"),
                        new ConsumerSource("src/Config/Input/Other/joystick/Other Controller.diff.lua", "return {}")
                    ])
                    .ToArray();
            var result = new ConsumerSourceImporter().Import(request with { Sources = sources });
            Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
            Assert.Equal(5, result.Configuration!.Profiles.Count);
            Assert.Equal(request.Sources.Count + 2, result.Provenance.Count);
            Assert.Contains(result.Diagnostics, x => x.Code == "unreferenced-module-profiles");
            Assert.DoesNotContain(result.Configuration.Profiles, x => x.SourceName == "Old Controller.diff.lua");
        }
        /// <summary>A declared kneeboard profile still needs its Lua source when unrelated profiles are supplied.</summary>
        [Fact]
        public void Unreferenced_lua_cannot_replace_missing_declared_profile()
        {
            var request = Fixture();
            var missing = request.Sources.Single(x => x.Path.Contains("F16 MFD 1 ", StringComparison.Ordinal));
            var sources = request.Sources.Where(x => x != missing)
                              .Append(new ConsumerSource("src/Config/Input/UH-1H/joystick/Other Controller.diff.lua",
                                                         "return {}"))
                              .ToArray();
            var result = new ConsumerSourceImporter().Import(request with { Sources = sources });
            Assert.False(result.Succeeded);
            Assert.Contains(result.Diagnostics,
                            x => x.Message.Contains("no supplied Lua source", StringComparison.Ordinal));
        }
        /// <summary>Two logical profiles cannot claim the same authored Lua file.</summary>
        [Fact]
        public void Duplicate_profile_path_is_rejected()
        {
            var request = Fixture();
            var sources = Replace(request, "config/kneeboard.json",
                                  x =>
                                  {
                                      var root = JsonNode.Parse(x)!;
                                      var profiles = root["profiles"]!;
                                      profiles["tm-mfd-2"] = profiles["tm-mfd-1"]!.GetValue<string>();
                                      return root.ToJsonString();
                                  });
            var result = new ConsumerSourceImporter().Import(request with { Sources = sources });
            Assert.False(result.Succeeded);
            Assert.Contains(result.Diagnostics,
                            x => x.Message.Contains("Duplicate kneeboard profile path", StringComparison.Ordinal));
        }
        /// <summary>An override cannot refer to a device profile that was not imported.</summary>
        [Fact]
        public void Orphan_device_override_is_rejected()
        {
            var request = Fixture();
            var sources = Replace(request, "config/scaffold-device-overrides.json",
                                  x =>
                                  {
                                      var root = JsonNode.Parse(x)!;
                                      root["Other.diff.lua"] = "tm-mfd";
                                      return root.ToJsonString();
                                  });
            var result = new ConsumerSourceImporter().Import(request with { Sources = sources });
            Assert.False(result.Succeeded);
            Assert.Contains(result.Diagnostics, x => x.Message.Contains("Device override references unknown profile",
                                                                        StringComparison.Ordinal));
        }
        /// <summary>A conflicting MFD instance role is reported before projection.</summary>
        [Fact]
        public void Conflicting_instance_role_is_rejected()
        {
            var request = Fixture();
            var sources = Replace(request, "config/scaffold-instance-roles.json",
                                  x => x.Replace("\"MFD3\"", "\"MFD1\"", StringComparison.Ordinal));
            var result = new ConsumerSourceImporter().Import(request with { Sources = sources });
            Assert.False(result.Succeeded);
            Assert.Contains(result.Diagnostics, x => x.Message.Contains("Instance role", StringComparison.Ordinal));
        }
        /// <summary>Page devices must resolve against the shared hardware manifest.</summary>
        [Fact]
        public void Unknown_page_device_is_rejected()
        {
            var request = Fixture();
            var sources = Replace(request, "config/kneeboard.json",
                                  x =>
                                  {
                                      var root = JsonNode.Parse(x)!;
                                      root["pages"]![0]!["deviceId"] = "invented-device";
                                      return root.ToJsonString();
                                  });
            var result = new ConsumerSourceImporter().Import(request with { Sources = sources });
            Assert.False(result.Succeeded);
            Assert.Contains(result.Diagnostics,
                            x => x.Message.Contains("Unknown shared device identity", StringComparison.Ordinal));
        }
        /// <summary>Malformed module-owned Lua cannot produce a partial configuration.</summary>
        [Fact]
        public void Invalid_modifiers_lua_is_atomic()
        {
            var request = Fixture();
            var sources = Replace(request, "src/Config/Input/UH-1H/modifiers.lua",
                                  _ => "return {}");
            var result = new ConsumerSourceImporter().Import(request with { Sources = sources });
            Assert.False(result.Succeeded);
            Assert.Empty(result.Provenance);
            Assert.Contains(result.Diagnostics, x => x.Code == "invalid-huey-source");
        }
        /// <summary>Every expected file is needed to prove the full Huey import.</summary>
        [Fact]
        public void Missing_config_file_is_reported()
        {
            var request = Fixture();
            var sources = request.Sources.Where(x => x.Path != "config/ui-layer-overlay.json").ToArray();
            var result = new ConsumerSourceImporter().Import(request with { Sources = sources });
            Assert.True(result.Succeeded);
            Assert.False(result.Metadata.ContainsKey("config/ui-layer-overlay.json"));
        }
        /// <summary>Unknown source paths are rejected before any consumer files are read.</summary>
        [Fact]
        public void Unknown_source_path_is_rejected()
        {
            var request = Fixture();
            var result = new ConsumerSourceImporter().Import(
                request with { Sources =
                                   [..request.Sources, new("src/Config/Input/Other/unknown.diff.lua", "return {}")] });
            Assert.False(result.Succeeded);
            Assert.Contains(result.Diagnostics, x => x.Code == "invalid-path");
        }
        /// <summary>Uses another module's own scaffold and kneeboard paths without Huey-only support files.</summary>
        [Fact]
        public void Consumer_module_identity_is_derived_from_its_own_config()
        {
            var original = Fixture();
            static string Rewrite(ConsumerSource source)
            {
                var text = source.Path.EndsWith(".json", StringComparison.Ordinal)
                               ? source.Content.Replace("UH-1H", "F-14BU", StringComparison.Ordinal)
                               : source.Content;
                if (source.Path != "config/kneeboard.json")
                    return text;
                var json = JsonNode.Parse(text)!;
                json.AsObject().Remove("uiLayerUtilization");
                return json.ToJsonString();
            }
            var sources =
                original.Sources
                    .Where(x => x.Path is not("config/scaffold.dcs-scaffold.json" or "config/ui-layer-overlay.json" or
                                              "config/scaffold-device-overrides.json" or
                                              "config/scaffold-instance-roles.json"))
                    .Select(x => new ConsumerSource(x.Path.Replace("UH-1H", "F-14BU", StringComparison.Ordinal),
                                                    Rewrite(x)))
                    .ToArray();
            var result =
                new ConsumerSourceImporter().Import(original with { Sources = sources, Repository = "F-14BU" });
            Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
            Assert.Equal("F-14BU", result.Configuration!.Module.DcsId);
            Assert.Equal(5, result.Configuration.Profiles.Count);
            Assert.Equal(101, KneeboardLabelImporter.Import(result.Metadata, result.Configuration).Count);
            Assert.Empty(result.Usage);
            Assert.All(result.Provenance, x => Assert.Equal("F-14BU", x.Repository));
        }
        /// <summary>Retains module-specific overlay and repeated control entries for later editing support.</summary>
        [Fact]
        public void Repeated_control_bindings_and_overlay_are_preserved()
        {
            var request = Fixture();
            var sources = request.Sources
                              .Select(source =>
                                      {
                                          if (source.Path == "config/kneeboard.json")
                                          {
                                              var root = JsonNode.Parse(source.Content)!;
                                              var controls = root["pages"]![0]!["controls"]!;
                                              var original = controls["mfd-osb-t3"]!.DeepClone();
                                              controls["mfd-osb-t3"] = new JsonArray(original, original.DeepClone());
                                              return source with { Content = root.ToJsonString() };
                                          }
                                          if (source.Path == "config/ui-layer-overlay.json")
                                          {
                                              var root = JsonNode.Parse(source.Content)!;
                                              root["schemaVersion"] = 2;
                                              root["assignments"] = new JsonArray(
                                                  new JsonObject { ["clear"] = true, ["key"] = "JOY_BTN1" });
                                              return source with { Content = root.ToJsonString() };
                                          }
                                          return source;
                                      })
                              .ToArray();
            var imported = new ConsumerSourceImporter().Import(request with { Sources = sources });
            Assert.True(imported.Succeeded, string.Join("; ", imported.Diagnostics.Select(x => x.Message)));
            Assert.Contains(imported.Diagnostics, x => x.Code == "module-overlay-preserved");
            Assert.Equal(101, KneeboardLabelImporter.Import(imported.Metadata, imported.Configuration!).Count);
            Assert.Contains("\"schemaVersion\":2", imported.Sources["config/ui-layer-overlay.json"],
                            StringComparison.Ordinal);
        }
        /// <summary>Declared pages with removed-only profiles are omitted like DCS-Common's generated PNGs.</summary>
        [Fact]
        public void Removed_only_profile_has_no_rendered_device_page()
        {
            var request = Fixture();
            var source = request.Sources.Single(x => x.Path.Contains("F16 MFD 1 ", StringComparison.Ordinal));
            const string removedOnly = """
               local diff = {
                 ["axisDiffs"] = {
                   ["a2001cdnil"] = {
                     ["removed"] = { [1] = { ["key"] = "JOY_Y" } },
                     ["name"] = "Pitch",
                   },
                 },
               }
               return diff
               """;
            var sources = request.Sources.Select(x => x == source ? x with { Content = removedOnly } : x).ToArray();
            var result = new ConsumerSourceImporter().Import(request with { Sources = sources });
            Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
            Assert.Equal(5, result.Configuration!.Profiles.Count);
            Assert.Contains(result.Configuration.Bindings,
                            x => x.Disposition == BindingDisposition.Removed &&
                                 x.ProfileId == result.Configuration.Profiles.Single(y => y.Instance == "MFD1").Id);
            Assert.DoesNotContain(result.Pages, x => x.StableKey == "01-TM-MFD-1");
            Assert.Contains(result.Diagnostics, x => x.Code == "inactive-kneeboard-page" &&
                                                     x.Message.Contains("01-TM-MFD-1", StringComparison.Ordinal));
            Assert.Equal(5, result.Pages.Count);
        }

        /// <summary>Saved repo-input variants do not make the canonical module solution ambiguous.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Multiple_consumer_solutions_use_canonical_solution_and_preserve_variants(bool reverse)
        {
            var request = Fixture();
            var canonical = request.Sources.Single(x => x.Path == "config/UH-1H.dcs-scaffold.json");
            var alternate = JsonNode.Parse(canonical.Content)!;
            alternate["import"]!["displayName"] = "Alternate saved solution";
            alternate["import"]!["profilesDirectory"] = "repo/joystick";
            var variant = new ConsumerSource("config/UH-1H.repo.dcs-scaffold.dcs-scaffold.json", alternate.ToJsonString());
            ConsumerSource[] sources = [..request.Sources, variant];
            if (reverse) Array.Reverse(sources);

            var imported = new ConsumerSourceImporter().Import(request with { Sources = sources });
            var expected = new ConsumerSourceImporter().Import(request);
            Assert.True(imported.Succeeded, string.Join("; ", imported.Diagnostics.Select(x => x.Message)));
            Assert.Equal(expected.Configuration!.Module.DisplayName, imported.Configuration!.Module.DisplayName);
            Assert.Equal(expected.Configuration.Modifiers, imported.Configuration.Modifiers);
            Assert.Equal(variant.Content, imported.Sources[variant.Path]);
            Assert.Contains(imported.Provenance, x => x.Path == variant.Path);
        }
        /// <summary>Without a canonical filename, matching saved solutions have stable path precedence.</summary>
        [Fact]
        public void Multiple_named_solutions_without_canonical_name_have_deterministic_selection()
        {
            var request = Fixture();
            var canonical = request.Sources.Single(x => x.Path == "config/UH-1H.dcs-scaffold.json");
            var first = canonical with { Path = "config/A.dcs-scaffold.json" };
            var alternate = JsonNode.Parse(canonical.Content)!;
            alternate["import"]!["displayName"] = "Alternate saved solution";
            var second = new ConsumerSource("config/Z.dcs-scaffold.json", alternate.ToJsonString());
            var remaining = request.Sources.Where(x => x != canonical).ToArray();
            var forward = new ConsumerSourceImporter().Import(request with { Sources = [first, second, ..remaining] });
            var reverse = new ConsumerSourceImporter().Import(request with { Sources = [second, first, ..remaining] });
            Assert.True(forward.Succeeded);
            Assert.True(reverse.Succeeded);
            Assert.Equal(forward.Configuration!.Module, reverse.Configuration!.Module);
            Assert.NotEqual("Alternate saved solution", forward.Configuration.Module.DisplayName);
        }
        /// <summary>Invalid kneeboard shapes fail atomically with the existing diagnostic contract.</summary>
        [Theory]
        [InlineData("profiles", "[]", "Kneeboard profiles must be a keyed object.")]
        [InlineData("pages", "{}", "Kneeboard pages must be an array.")]
        [InlineData("labels", "[]", "Binding labels must be a keyed object.")]
        [InlineData("schemaVersion", "2", "Kneeboard schema, aircraft, or modifiers reference")]
        [InlineData("modifiersFile", "\"src/Config/Input/Other/modifiers.lua\"", "Kneeboard schema, aircraft, or modifiers reference")]
        [InlineData("aircraft", "\" \"", "Missing aircraft.")]
        public void Invalid_kneeboard_shapes_preserve_atomic_failure(string property, string value, string message)
        {
            var request = Fixture();
            var sources = Replace(request, "config/kneeboard.json", text =>
            {
                var root = JsonNode.Parse(text)!;
                root[property] = JsonNode.Parse(value);
                return root.ToJsonString();
            });
            var result = new ConsumerSourceImporter().Import(request with { Sources = sources });
            AssertAtomicFailure(result, message);
        }

        /// <summary>Optional source metadata cannot silently introduce unsupported schemas or decision shapes.</summary>
        [Theory]
        [InlineData("config/ui-layer-overlay.json", "{\"schemaVersion\":3}", "Unsupported module-owned UI Layer overlay schema.")]
        [InlineData("config/ui-layer-overlay.json", "[]", "Unsupported module-owned UI Layer overlay schema.")]
        [InlineData("config/scaffold.dcs-scaffold.json", "{\"schemaVersion\":2}", "UI Layer scaffold must use schema v1")]
        [InlineData("config/scaffold-device-overrides.json", "[]", "Device overrides and instance roles must be keyed objects.")]
        [InlineData("config/scaffold-instance-roles.json", "[]", "Device overrides and instance roles must be keyed objects.")]
        public void Invalid_optional_metadata_preserves_atomic_failure(string path, string value, string message)
        {
            var request = Fixture();
            var sources = Replace(request, path, _ => value);
            AssertAtomicFailure(new ConsumerSourceImporter().Import(request with { Sources = sources }), message);
        }

        /// <summary>A device UI usage entry must reference an imported physical instance.</summary>
        [Fact]
        public void Ui_usage_without_matching_instance_is_atomic()
        {
            var request = Fixture();
            var sources = Replace(request, "config/kneeboard.json", text =>
            {
                var root = JsonNode.Parse(text)!;
                root["uiLayerUtilization"]!["bindings"]![0]!["deviceInstance"] = "unknown-instance";
                return root.ToJsonString();
            });
            AssertAtomicFailure(new ConsumerSourceImporter().Import(request with { Sources = sources }),
                                "UI Layer utilization has no matching module device instance");
        }

        /// <summary>Implicit UI Layer usage modes must remain unsupported.</summary>
        [Fact]
        public void Nonexplicit_ui_usage_is_atomic()
        {
            var request = Fixture();
            var sources = Replace(request, "config/kneeboard.json", text =>
            {
                var root = JsonNode.Parse(text)!;
                root["uiLayerUtilization"]!["mode"] = "implicit";
                return root.ToJsonString();
            });
            AssertAtomicFailure(new ConsumerSourceImporter().Import(request with { Sources = sources }),
                                "Only explicit UI Layer utilization is supported.");
        }

        /// <summary>Upfront request errors retain their order and return no parsed state.</summary>
        [Fact]
        public void Request_validation_accumulates_errors_in_original_order()
        {
            var request = Fixture() with { Commit = "invalid", Catalog = null!, SharedLua = null!, Sources = [] };
            var result = new ConsumerSourceImporter().Import(request);
            Assert.Equal(RequestDiagnosticCodes,
                         result.Diagnostics.Select(x => x.Code));
            Assert.Null(result.Configuration);
            Assert.Empty(result.Sources);
            Assert.Empty(result.Provenance);
        }

        /// <summary>Source ordering cannot change any normalized result, warning, metadata, or content hash.</summary>
        [Fact]
        public void Source_permutations_preserve_complete_import_result()
        {
            var request = Fixture();
            var importer = new ConsumerSourceImporter();
            var forward = importer.Import(request);
            var reversed = importer.Import(request with { Sources = request.Sources.Reverse().ToArray() });
            Assert.True(forward.Succeeded);
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(forward),
                         System.Text.Json.JsonSerializer.Serialize(reversed));
        }

        /// <summary>Each unsafe source-path family is rejected before parsing or retaining partial state.</summary>
        [Theory]
        [InlineData("")]
        [InlineData("/README.md")]
        [InlineData("docs\\notes.md")]
        [InlineData("docs/../notes.md")]
        [InlineData("docs/./notes.md")]
        [InlineData("docs//notes.md")]
        [InlineData("assets/unknown.png")]
        public void Unsafe_source_paths_preserve_atomic_failure(string path)
        {
            var request = Fixture();
            var result = new ConsumerSourceImporter().Import(request with
            {
                Sources = [..request.Sources, new(path, "source text")]
            });
            Assert.False(result.Succeeded);
            Assert.Contains(result.Diagnostics, x => x.Code == "invalid-path" && x.IsError);
            Assert.Null(result.Configuration);
            Assert.Empty(result.Sources);
            Assert.Empty(result.Metadata);
            Assert.Empty(result.Provenance);
        }

        /// <summary>Splitting path recognition preserves all previously supported release script families.</summary>
        [Theory]
        [InlineData("scripts/Build-Release.ps1")]
        [InlineData("scripts/Test-Package.ps1")]
        [InlineData("scripts/version.mjs")]
        public void Release_script_paths_are_preserved_as_pinned_text(string path)
        {
            var request = Fixture();
            const string text = "authored release script text";
            var result = new ConsumerSourceImporter().Import(request with
            {
                Sources = [..request.Sources, new(path, text)]
            });
            Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
            Assert.Equal(text, result.Sources[path]);
            Assert.Contains(result.Provenance, x => x.Path == path);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static readonly string[] RequestDiagnosticCodes = ["invalid-commit", "invalid-shared-input", "missing-sources"];
        private static void AssertAtomicFailure(ConsumerImportResult result, string message)
        {
            Assert.False(result.Succeeded);
            Assert.Null(result.Configuration);
            Assert.Empty(result.Instances);
            Assert.Empty(result.Pages);
            Assert.Empty(result.Usage);
            Assert.Empty(result.UiFunctionKeys);
            Assert.Empty(result.Metadata);
            Assert.Empty(result.Sources);
            Assert.Empty(result.Provenance);
            Assert.Contains(result.Diagnostics, x => x.Code == "invalid-huey-source" && x.IsError &&
                                                     x.Message.Contains(message, StringComparison.Ordinal));
        }
        private static ConsumerSource[] Replace(ConsumerImportRequest request, string path,
                                                Func<string, string> transform) =>
            request.Sources.Select(x => x.Path == path ? x with { Content = transform(x.Content) } : x).ToArray();
        private static ConsumerImportRequest Fixture()
        {
            var root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DcsCommon");
            string Read(string name) => File.ReadAllText(Path.Combine(root, name));
            var catalog = new DcsCommonCatalogImporter().Import(
                new("dd3d9cff8997c57ebfad36066ec4caf06bdbe52e", "assets/shared/hardware/manifest.json",
                    Read("hardware.json"), "assets/shared/ui-layer/functions.json", Read("functions.json"),
                    "assets/shared/ui-layer/hardware-overlays.json", Read("hardware-overlays.json"),
                    "assets/shared/hardware/scaffold-device-map.json", Read("device-map.json")));
            Assert.True(catalog.Succeeded);
            var common = Path.Combine(root, "Lua");
            var lua = Directory.GetFiles(common, "*.lua", SearchOption.AllDirectories)
                          .Select(x => new SharedLuaSource(Path.GetRelativePath(common, x).Replace('\\', '/'),
                                                           File.ReadAllText(x)))
                          .ToArray();
            var shared =
                new DcsCommonLuaImporter().Import(new("dd3d9cff8997c57ebfad36066ec4caf06bdbe52e", catalog, lua));
            Assert.True(shared.Succeeded);
            var sourceRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Huey");
            var sources = Directory.GetFiles(sourceRoot, "*.*", SearchOption.AllDirectories)
                              .Select(x => new ConsumerSource(Path.GetRelativePath(sourceRoot, x).Replace('\\', '/'),
                                                              File.ReadAllText(x)))
                              .OrderBy(x => x.Path, StringComparer.Ordinal)
                              .ToArray();
            return new("77d924cce220355860e8f026cff3524bac531e29", catalog, shared, sources);
        }
        #endregion END private helpers
    }
}
