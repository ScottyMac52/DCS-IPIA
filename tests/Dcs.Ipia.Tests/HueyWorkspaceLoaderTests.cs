using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Exercises the desktop loader against real temporary Git checkouts.</summary>
    public sealed class HueyWorkspaceLoaderTests
    {
        #region BEGIN Public
        /// <summary>Only committed source is imported even when a checkout has uncommitted changes.</summary>
        [Fact]
        public async Task Loads_committed_huey_and_common_sources()
        {
            var temp = Path.Combine(Path.GetTempPath(), "DcsIpiaWorkspace-" + Guid.NewGuid().ToString("N"));
            var common = Path.Combine(temp, "common");
            var huey = Path.Combine(temp, "huey");
            try
            {
                Directory.CreateDirectory(common);
                Directory.CreateDirectory(huey);
                var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
                var shared = Path.Combine(fixtures, "DcsCommon");
                foreach (var (name, path)
                             in new[] { ("hardware.json", "assets/shared/hardware/manifest.json"),
                                        ("functions.json", "assets/shared/ui-layer/functions.json"),
                                        ("hardware-overlays.json", "assets/shared/ui-layer/hardware-overlays.json"),
                                        ("device-map.json", "assets/shared/hardware/scaffold-device-map.json") })
                    Copy(Path.Combine(shared, name), common, path);
                var lua = Path.Combine(shared, "Lua");
                foreach (var source in Directory.GetFiles(lua, "*.lua", SearchOption.AllDirectories))
                    Copy(source, common, Path.GetRelativePath(lua, source));
                var consumer = Path.Combine(fixtures, "Huey");
                foreach (var source in Directory.GetFiles(consumer, "*.*", SearchOption.AllDirectories))
                    Copy(source, huey, Path.GetRelativePath(consumer, source));
                var image = Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/ZQAAAABJRU5ErkJggg==");
                var imagePath = Path.Combine(huey, "kneeboard", "UH-1H", "00-UH1H-CONTROL-OVERVIEW.png");
                Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
                File.WriteAllBytes(imagePath, image);
                using (var overlays = JsonDocument.Parse(
                           File.ReadAllText(Path.Combine(common, "assets/shared/ui-layer/hardware-overlays.json"))))
                {
                    foreach (var device in new[] { "tm-mfd", "grip-f18c", "tm-warthog-throttle", "tm-warthog-grip" })
                    {
                        var controls = device == "tm-warthog-throttle" ? new[] { "warthog-thr-slew-push" }
                                                                       : overlays.RootElement.GetProperty("devices")
                                                                             .GetProperty(device)
                                                                             .GetProperty("bindings")
                                                                             .EnumerateObject()
                                                                             .Select(x => x.Value.GetString()!)
                                                                             .Distinct()
                                                                             .ToArray();
                        var rects = string.Join(
                            "\n",
                            controls.Select(
                                (control, i) =>
                                    $"  <!-- box:{control} -->\n  <rect x='10' y='{10+i*48}' width='55' height='45'/>\n  <text id='lbl-{control}' x='10' y='10'>label</text>"));
                        var path = Path.Combine(common, "assets", "shared", "hardware", "svg", device + ".svg");
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        var mime = device == "grip-f18c" ? "jpeg" : "png";
                        File.WriteAllText(
                            path,
                            $"<svg xmlns='http://www.w3.org/2000/svg' width='960' height='720' viewBox='0 0 960 720'>\n  <image href='data:image/{mime};base64,{Convert.ToBase64String(image)}' x='0' y='0' width='960' height='720'/>\n{rects}\n</svg>");
                    }
                }
                var catalogPath = Path.Combine(temp, "UH-1H-commands.json");
                File.WriteAllText(catalogPath, System.Text.Json.JsonSerializer.Serialize(new {
                    schemaVersion = 1, moduleId = "UH-1H",
                    commands =
                        new[] { new { bindingKey = "d3002pnilunilcd27vd-1vpnilvunil", name = "ADF Band 190-400 kHz",
                                      categoryPath = AdfCategory, type = "button" },
                                new { bindingKey = "d1702pnilunilcdnilvdnilvpnilvunil",
                                      name = "Camera bookmarks editor", categoryPath = ViewCategory, type = "button" } }
                }));
                var solutionPath = Path.Combine(huey, "config", "UH-1H.dcs-scaffold.json");
                var solution = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(solutionPath))!;
                solution["import"]!["commandSource"] = System.Text.Json.JsonSerializer.SerializeToNode(
                    new { type = "catalog-json", paths = new[] { catalogPath } });
                File.WriteAllText(solutionPath, solution.ToJsonString());
                foreach (var (path, content)
                             in new[] { ("README.md", "# committed module readme\n"),
                                        ("docs/INSTALLATION.md", "Committed installation notes\n"),
                                        ("packaging/ovgme/README.TXT", "Committed package instructions\n"),
                                        ("scripts/Build-OvGME.ps1", "Write-Output 'committed build'\n"),
                                        (".github/workflows/release.yml", "name: Committed release\n") })
                {
                    var target = Path.Combine(huey, path);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.WriteAllText(target, content);
                }
                Commit(common);
                Commit(huey);
                File.WriteAllText(Path.Combine(huey, "README.md"), "# uncommitted readme\n");
                File.WriteAllText(Path.Combine(huey, "config", "kneeboard.json"), "{broken");
                var result = await new ModuleWorkspaceLoader().LoadAsync(common, huey);
                Assert.True(result.Import.Succeeded,
                            string.Join("; ", result.Import.Diagnostics.Select(x => x.Message)));
                Assert.Contains("config/kneeboard.json", result.UncommittedSourcePaths);
                Assert.Contains("README.md", result.UncommittedSourcePaths);
                Assert.Contains(result.Import.Diagnostics, x => x.Code == "uncommitted-module-sources" && !x.IsError);
                Assert.Equal(result.Import.Provenance[0].Commit, result.ModuleCommit);
                Assert.Equal(5, result.Import.Configuration!.Profiles.Count);
                Assert.Equal(17, result.Import.Provenance.Count);
                Assert.Equal("# committed module readme\n", result.Import.Sources["README.md"]);
                Assert.Equal("Committed installation notes\n", result.Import.Sources["docs/INSTALLATION.md"]);
                Assert.Contains(result.Import.Provenance, x => x.Path == ".github/workflows/release.yml");
                Assert.Contains(result.Import.Provenance, x => x.Path == "scripts/Build-OvGME.ps1");
                Assert.Equal(13, result.Import.UiFunctionKeys.Count);
                Assert.Contains("assets/shared/ui-layer/input/UiLayer/modifiers.lua", result.SharedLuaSources.Keys);
                Assert.Contains(result.SharedLuaSources.Keys, x => x.EndsWith(".diff.lua", StringComparison.Ordinal));
                Assert.DoesNotContain(result.SharedLuaSources.Keys,
                                      x => x.StartsWith("assets/shared/hardware/", StringComparison.Ordinal));
                var authoredLabels =
                    KneeboardLabelImporter.Import(result.Import.Metadata, result.Import.Configuration!);
                Assert.Equal(101, authoredLabels.Count);
                var mfd1 =
                    result.Import.Configuration!.Profiles.Single(x => x.DeviceId == "tm-mfd" && x.Instance == "MFD1");
                var mfd1Top = new DraftAssignmentSlot(result.Import.Configuration.Module.Id, mfd1.Id,
                                                      AssignmentLayer.Module, BindingSection.Key, "JOY_BTN1");
                Assert.Equal("Switch", authoredLabels[mfd1Top.Key]);
                var hornet = result.Import.Configuration!.Profiles.Single(x => x.DeviceId == "moza-ab9-hornet-grip");
                var baseArm = new DraftAssignmentSlot(result.Import.Configuration.Module.Id, hornet.Id,
                                                      AssignmentLayer.Module, BindingSection.Key, "JOY_BTN11");
                Assert.Equal("Arm Up", authoredLabels[baseArm.Key]);
                Assert.Equal("Gun Up", authoredLabels[(baseArm with { ModifierChord = "MOZA_F16_F18_BTN3" }).Key]);
                var baseTrim = baseArm with { InputKey = "JOY_BTN_POV1_U" };
                Assert.Equal("Pilot", authoredLabels[baseTrim.Key]);
                Assert.Equal("Pilot Sight Elevation Increase",
                             authoredLabels[(baseTrim with { ModifierChord = "MOZA_F16_F18_BTN3" }).Key]);
                var database = "Data Source=" + Path.Combine(temp, "labels.db") + ";Pooling=False";
                await new SqliteMigrationRunner(database).MigrateAsync();
                await new SqliteConfigurationRepository(database).ReplaceAsync(result.Import.Configuration);
                var workspace =
                    new EditorWorkspaceSnapshot(result.Import.Configuration, result.Import.Pages, result.Images,
                                                result.Diagrams, [], result.UiCommands, [], authoredLabels,
                                                result.Import.Sources.Concat(result.SharedLuaSources)
                                                    .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal));
                var repository = new SqliteEditorWorkspaceRepository(database);
                await repository.SaveAsync(workspace);
                var reopened = await repository.GetAsync(result.Import.Configuration.Module.Id);
                Assert.NotNull(reopened);
                Assert.Equal(authoredLabels.Count, reopened.Labels.Count);
                Assert.Equal("Switch", reopened.Labels[mfd1Top.Key]);
                Assert.Equal("Arm Up", reopened.Labels[baseArm.Key]);
                Assert.Equal("Gun Up", reopened.Labels[(baseArm with { ModifierChord = "MOZA_F16_F18_BTN3" }).Key]);
                Assert.Equal(result.SharedLuaSources["assets/shared/ui-layer/input/UiLayer/modifiers.lua"],
                             reopened.Sources!["assets/shared/ui-layer/input/UiLayer/modifiers.lua"]);
                Assert.Contains("config/ipia-device-map.json", reopened.Sources.Keys);
                Assert.Equal(result.SharedLuaSources["config/ipia-hardware.json"], reopened.Sources["config/ipia-hardware.json"]);
                Assert.DoesNotContain(reopened.Configuration.Profiles, x => x.DeviceId == "ava-base-f16c");
                var ava = Assert.Single(WorkspaceStructureEditor.Hardware(reopened), x => x.DeviceId == "ava-base-f16c");
                Assert.NotEmpty(ava.Diagram.Controls);
                Assert.Equal("ava-base-f16c", DetectedDeviceHardwareMatcher.Match(reopened,
                    new(Guid.NewGuid(), "AVA [R] - Viper", true))!.DeviceId);
                Assert.Equal("# committed module readme\n", reopened.Sources["README.md"]);
                Assert.Equal("Committed package instructions\n", reopened.Sources["packaging/ovgme/README.TXT"]);
                var frozen = EditorSnapshotSerializer.Serialize(reopened);
                Assert.Equal(reopened.Sources!["assets/shared/ui-layer/input/UiLayer/modifiers.lua"],
                             EditorSnapshotSerializer.Deserialize(frozen)
                                 .Sources!["assets/shared/ui-layer/input/UiLayer/modifiers.lua"]);
                Assert.Equal("Write-Output 'committed build'\n",
                             EditorSnapshotSerializer.Deserialize(frozen).Sources!["scripts/Build-OvGME.ps1"]);
                Assert.StartsWith("{\n", result.Import.Sources["config/kneeboard.json"], StringComparison.Ordinal);
                var preview = Assert.Single(result.Images);
                Assert.Equal("00-UH1H-CONTROL-OVERVIEW", preview.PageKey);
                Assert.Equal(image, preview.Bytes);
                Assert.Equal(2, result.ModuleCommands.Count);
                Assert.Contains(result.ModuleCommands,
                                x => x.Name == "ADF Band 190-400 kHz" && x.Category == "ADF Set Control panel");
                Assert.Single(CommandPalette.Filter(result.ModuleCommands, null, "View"));
                Assert.Empty(result.CommandDiagnostics);
                var savedCatalog = File.ReadAllText(catalogPath);
                File.Delete(catalogPath);
                var missingSource = await new ModuleWorkspaceLoader().LoadAsync(common, huey);
                Assert.Empty(missingSource.ModuleCommands);
                Assert.Contains(missingSource.CommandDiagnostics, x => x.Code == "module-command-source" && x.IsError);
                File.WriteAllText(catalogPath, "{invalid");
                var invalidSource = await new ModuleWorkspaceLoader().LoadAsync(common, huey);
                Assert.Contains(invalidSource.CommandDiagnostics, x => x.Code == "module-command-source" && x.IsError);
                File.WriteAllText(catalogPath, savedCatalog);
                Assert.NotEmpty(result.UiCommands);
                Assert.Contains(result.UiCommands, x => x.Name == "VR Zoom");
                Assert.Equal(5, result.Diagrams.Count);
                Assert.All(result.Diagrams, x => Assert.Contains("<svg", x.Svg, StringComparison.Ordinal));
                Assert.All(result.Diagrams, x => Assert.NotEmpty(x.Controls));
                Assert.Equal(13, result.Diagrams.Single(x => x.Instance == "MFD3").Callouts.Count);
                Assert.Equal(3, result.Diagrams.Single(x => x.Path.EndsWith("/grip-f18c.svg", StringComparison.Ordinal))
                                    .Callouts.Count);
                Assert.Contains(
                    result.Diagrams.Single(x => x.Path.EndsWith("/grip-f18c.svg", StringComparison.Ordinal)).Callouts,
                    x => x.Control == "hornet-grip-paddle" && x.Function == "Recenter VR" && x.X == 10);
                Assert.Contains(result.Diagrams, x => x.Instance == "MFD1");
                Assert.Contains(result.Diagrams, x => x.Instance == "MFD2");
                Assert.Contains(result.Diagrams,
                                x => x.Path.EndsWith("/tm-warthog-throttle.svg", StringComparison.Ordinal) &&
                                     !x.SupportsUiLayer && x.Controls.Any(y => y.Control == "warthog-thr-slew-push"));
                Assert.All(result.Diagrams, x => Assert.Equal(image, x.Background));
                Assert.Contains(result.Diagrams.Single(x => x.Instance == "MFD3").Controls,
                                x => x.Control == "mfd-osb-t1-shifted" && x.InputKey == "JOY_BTN1");
                Assert.Contains(result.Diagrams.Single(x => x.Instance == "MFD3").Callouts,
                                x => x.Function == "VR Zoom" && x.Modifier == "MOZA_MODIFIER_BTN3");
                var paths = new[] { "assets/shared/hardware/manifest.json", "assets/shared/ui-layer/functions.json",
                                    "assets/shared/ui-layer/hardware-overlays.json",
                                    "assets/shared/hardware/scaffold-device-map.json" };
                var texts = paths.Select(x => File.ReadAllText(Path.Combine(common, x))).ToArray();
                var catalog =
                    new DcsCommonCatalogImporter().Import(new(new string('a', 40), paths[0], texts[0], paths[1],
                                                              texts[1], paths[2], texts[2], paths[3], texts[3]));
                Assert.True(catalog.Succeeded);
                var graphics =
                    new Dictionary<string, string> { ["assets/shared/hardware/svg/tm-mfd.svg"] = File.ReadAllText(
                                                         Path.Combine(common, "assets/shared/hardware/svg/tm-mfd.svg")),
                                                     ["assets/shared/hardware/svg/grip-f18c.svg"] =
                                                         File.ReadAllText(Path.Combine(
                                                             common, "assets/shared/hardware/svg/grip-f18c.svg")),
                                                     ["assets/shared/hardware/svg/tm-warthog-throttle.svg"] =
                                                         File.ReadAllText(Path.Combine(
                                                             common,
                                                             "assets/shared/hardware/svg/tm-warthog-throttle.svg")) };
                var withoutHardwareMap = UiLayerDiagramFactory.Create(result.Import, catalog, graphics);
                Assert.Equal(5, withoutHardwareMap.Count);
                Assert.All(withoutHardwareMap, x => Assert.Empty(x.Controls));
                Assert.Contains(withoutHardwareMap, x => x.Instance == "MFD1" && !x.SupportsUiLayer);
                var mappedControl = Assert.Single(result.Diagrams.Single(x => x.Instance == "MFD3").Controls.Take(1));
                var mfdDevice = catalog.Devices.Single(x => x.StableKey == "tm-mfd");
                var baseControl = mappedControl.Control.EndsWith("-shifted", StringComparison.Ordinal)
                                      ? mappedControl.Control[..^ "-shifted".Length]
                                      : mappedControl.Control;
                var axisControl = new DeviceControl(Guid.NewGuid(), mfdDevice.Id, baseControl, mappedControl.Label,
                                                    ControlKind.Axis, null);
                var canonicalLua = new DcsCommonLuaImporter().Import(
                    new(new string('a', 40), catalog,
                        Directory.GetFiles(lua, "*.lua", SearchOption.AllDirectories)
                            .Select(path => new SharedLuaSource(Path.GetRelativePath(lua, path).Replace('\\', '/'),
                                                                File.ReadAllText(path)))
                            .ToArray()));
                Assert.True(canonicalLua.Succeeded);
                var sharedMap = new SharedLuaImportResult(
                    canonicalLua.Controls
                        .Select(control => control.Control.StableKey == baseControl
                                               ? new SharedHardwareControl(axisControl, control.InputKey, "axis",
                                                                           control.Group, control.Position)
                                               : control)
                        .ToArray(),
                    canonicalLua.Profiles, canonicalLua.Bindings, canonicalLua.Modifiers, canonicalLua.Provenance,
                    canonicalLua.Diagnostics);
                var withoutLabel = new Dictionary<string, string>(graphics);
                withoutLabel["assets/shared/hardware/svg/tm-mfd.svg"] =
                    withoutLabel["assets/shared/hardware/svg/tm-mfd.svg"].Replace(
                        "id='lbl-" + mappedControl.Control + "'", "id='other-label'", StringComparison.Ordinal);
                var fallback = UiLayerDiagramFactory.Create(result.Import, catalog, withoutLabel, sharedMap);
                Assert.Contains(fallback.SelectMany(x => x.Controls), x => x.Control == mappedControl.Control &&
                                                                           x.Label == mappedControl.Control &&
                                                                           x.IsAxis);
                Assert.Throws<InvalidDataException>(
                    () => UiLayerDiagramFactory.Create(result.Import, catalog, new Dictionary<string, string>()));
                var missingBox = new Dictionary<string, string>(graphics);
                missingBox["assets/shared/hardware/svg/grip-f18c.svg"] =
                    missingBox["assets/shared/hardware/svg/grip-f18c.svg"].Replace(
                        "box:hornet-grip-paddle", "box:missing-control", StringComparison.Ordinal);
                Assert.Throws<InvalidDataException>(
                    () => UiLayerDiagramFactory.Create(result.Import, catalog, missingBox));
                var nonAdjacentBox = new Dictionary<string, string>(graphics);
                nonAdjacentBox["assets/shared/hardware/svg/grip-f18c.svg"] =
                    nonAdjacentBox["assets/shared/hardware/svg/grip-f18c.svg"].Replace(
                        "<!-- box:hornet-grip-paddle -->\n  <rect",
                        "<!-- box:hornet-grip-paddle -->\n  <text>Unexpected shape</text>\n  <rect",
                        StringComparison.Ordinal);
                var badBox = Assert.Throws<InvalidDataException>(
                    () => UiLayerDiagramFactory.Create(result.Import, catalog, nonAdjacentBox));
                Assert.Contains("adjacent rectangle", badBox.Message, StringComparison.Ordinal);
                var unsupportedImage = new Dictionary<string, string>(graphics);
                unsupportedImage["assets/shared/hardware/svg/tm-mfd.svg"] =
                    unsupportedImage["assets/shared/hardware/svg/tm-mfd.svg"].Replace(
                        "data:image/png;base64,", "data:image/gif;base64,", StringComparison.Ordinal);
                Assert.Throws<InvalidDataException>(
                    () => UiLayerDiagramFactory.Create(result.Import, catalog, unsupportedImage));
                var mfdSvg = graphics["assets/shared/hardware/svg/tm-mfd.svg"];
                foreach (var (original, invalid, expected) in new[] {
                             ("<svg xmlns='http://www.w3.org/2000/svg' width='960'", 
                              "<svg xmlns='http://www.w3.org/2000/svg' width='1000'", "SVG canvas"),
                             (" x='0' y='0' width='960' height='720'/>",
                              " x='950' y='0' width='960' height='720'/>", "embedded image"),
                             (" x='0' y='0' width='960' height='720'/>",
                              " x='NaN' y='0' width='960' height='720'/>", "embedded image"),
                             (" x='0' y='0' width='960' height='720'/>",
                              " x='0' y='0' width='0' height='720'/>", "embedded image")
                         })
                {
                    var malformed = new Dictionary<string, string>(graphics);
                    malformed["assets/shared/hardware/svg/tm-mfd.svg"] =
                        mfdSvg.Replace(original, invalid, StringComparison.Ordinal);
                    var errors = new List<string>();
                    var remaining = UiLayerDiagramFactory.Create(result.Import, catalog, malformed, sharedMap,
                        (profile, error) => errors.Add($"{profile.DeviceId}: {error.Message}"));
                    Assert.Equal(2, remaining.Count);
                    Assert.Contains(errors, x => x.Contains("tm-mfd.svg", StringComparison.Ordinal) &&
                                                  x.Contains(expected, StringComparison.Ordinal));
                }
                var insetImage = new Dictionary<string, string>(graphics);
                insetImage["assets/shared/hardware/svg/tm-mfd.svg"] = mfdSvg.Replace(
                    " x='0' y='0' width='960' height='720'/>",
                    " x='100' y='50' width='600' height='500'/>", StringComparison.Ordinal);
                Assert.Equal(5, UiLayerDiagramFactory.Create(result.Import, catalog, insetImage, sharedMap).Count);
                var throttleSvg = graphics["assets/shared/hardware/svg/tm-warthog-throttle.svg"];
                foreach (var (original, invalid) in new[] {
                             ("x='10'", "x='950'"), ("x='10'", "x='NaN'"),
                             ("x='10'", "x='-1'"), ("width='55'", "width='0'")
                         })
                {
                    var malformed = new Dictionary<string, string>(graphics);
                    malformed["assets/shared/hardware/svg/tm-warthog-throttle.svg"] =
                        throttleSvg.Replace(original, invalid, StringComparison.Ordinal);
                    var errors = new List<string>();
                    var remaining = UiLayerDiagramFactory.Create(result.Import, catalog, malformed, sharedMap,
                        (profile, error) => errors.Add($"{profile.DeviceId}: {error.Message}"));
                    Assert.Equal(4, remaining.Count);
                    Assert.Contains(errors, x => x.Contains("tm-warthog-throttle", StringComparison.Ordinal) &&
                                                  x.Contains("warthog-thr-slew-push", StringComparison.Ordinal));
                }
                var atOrigin = new Dictionary<string, string>(graphics);
                atOrigin["assets/shared/hardware/svg/tm-warthog-throttle.svg"] =
                    throttleSvg.Replace("x='10'", "x='0'", StringComparison.Ordinal);
                Assert.Contains(UiLayerDiagramFactory.Create(result.Import, catalog, atOrigin, sharedMap),
                                x => x.Controls.Any(control => control.Control == "warthog-thr-slew-push" &&
                                                             control.X == 0));
                Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(image)), preview.Sha256);
                Assert.Empty(result.GraphicDiagnostics);
                solution["import"]!["commandSource"] = null;
                File.WriteAllText(solutionPath, solution.ToJsonString());
                Git(huey, "add", "config/UH-1H.dcs-scaffold.json");
                Git(huey, "-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid", "commit", "-qm",
                    "unlink commands");
                var unlinked = await new ModuleWorkspaceLoader().LoadAsync(common, huey);
                Assert.Empty(unlinked.ModuleCommands);
                Assert.Empty(unlinked.CommandDiagnostics);
                solution["import"]!["commandSource"] = System.Text.Json.JsonSerializer.SerializeToNode(
                    new { type = "unsupported", paths = new[] { catalogPath } });
                File.WriteAllText(solutionPath, solution.ToJsonString());
                Git(huey, "add", "config/UH-1H.dcs-scaffold.json");
                Git(huey, "-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid", "commit", "-qm",
                    "invalid command source");
                var unsupported = await new ModuleWorkspaceLoader().LoadAsync(common, huey);
                Assert.Contains(unsupported.CommandDiagnostics, x => x.Code == "module-command-source" && x.IsError);
                var htmlPath = Path.Combine(temp, "MFD 1.html");
                File.WriteAllText(
                    htmlPath,
                    "<html><h1>MFD 1</h1><table><tr><td>JOY_BTN1</td><td>ADF Band 190-400 kHz</td><td>ADF Set Control panel</td><td>d3002pnilunilcd27vd-1vpnilvunil</td></tr></table></html>");
                solution["import"]!["commandSource"] = System.Text.Json.JsonSerializer.SerializeToNode(
                    new { type = "dcs-html", paths = new[] { htmlPath } });
                File.WriteAllText(solutionPath, solution.ToJsonString());
                Git(huey, "add", "config/UH-1H.dcs-scaffold.json");
                Git(huey, "-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid", "commit", "-qm",
                    "link DCS HTML");
                var linkedHtml = await new ModuleWorkspaceLoader().LoadAsync(common, huey);
                Assert.Contains(linkedHtml.ModuleCommands,
                                x => x.Category == "ADF Set Control panel" && x.Name == "ADF Band 190-400 kHz");
                Assert.Empty(linkedHtml.CommandDiagnostics);
                var throttlePath = Path.Combine(common, "assets/shared/hardware/svg/tm-warthog-throttle.svg");
                var throttleGraphic = File.ReadAllText(throttlePath);
                File.WriteAllText(throttlePath,
                                  throttleGraphic.Replace("box:warthog-thr-slew-push", "box:unmapped-hardware-control",
                                                          StringComparison.Ordinal));
                Git(common, "add", "--all");
                Git(common, "-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid", "commit", "-qm",
                    "unknown graphic control");
                var withUnknownControl = await new ModuleWorkspaceLoader().LoadAsync(common, huey);
                Assert.True(withUnknownControl.Import.Succeeded);
                Assert.Equal(4, withUnknownControl.Diagrams.Count);
                Assert.Contains(withUnknownControl.GraphicDiagnostics,
                                x => x.Code == "device-graphic-unavailable" && x.IsError &&
                                     x.Message.Contains("unmapped-hardware-control", StringComparison.Ordinal) &&
                                     x.Message.Contains("tm-warthog-throttle", StringComparison.Ordinal));
                File.WriteAllText(throttlePath, throttleGraphic);
                var gripPath = Path.Combine(common, "assets/shared/hardware/svg/grip-f18c.svg");
                File.WriteAllText(gripPath, "<svg broken");
                Git(common, "add", "--all");
                Git(common, "-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid", "commit", "-qm",
                    "broken graphic");
                var withBrokenGraphic = await new ModuleWorkspaceLoader().LoadAsync(common, huey);
                Assert.True(withBrokenGraphic.Import.Succeeded);
                Assert.Single(withBrokenGraphic.Images);
                Assert.Equal(result.UiCommands, withBrokenGraphic.UiCommands);
                Assert.Equal(4, withBrokenGraphic.Diagrams.Count);
                Assert.Contains(withBrokenGraphic.Diagrams,
                                x => x.Path.EndsWith("/tm-warthog-throttle.svg", StringComparison.Ordinal));
                Assert.Contains(withBrokenGraphic.GraphicDiagnostics,
                                x => x.Code == "device-graphic-unavailable" && x.IsError &&
                                     x.Message.Contains("moza-ab9-hornet-grip", StringComparison.Ordinal));
            }
            finally
            {
                DeleteFixture(temp);
            }
        }
        /// <summary>A committed consumer imports against packaged shared assets without a Common repository.</summary>
        [Fact]
        public async Task Imports_module_with_built_in_shared_assets()
        {
            var root = Path.Combine(Path.GetTempPath(), "DcsIpiaBundled-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Huey");
                foreach (var source in Directory.GetFiles(fixtures, "*", SearchOption.AllDirectories))
                    Copy(source, root, Path.GetRelativePath(fixtures, source));
                Commit(root);
                var preview = await new ModuleWorkspaceLoader().LoadAsync(BundledSharedCatalog.Root, root);
                Assert.True(preview.Import.Succeeded, string.Join("; ", preview.Import.Diagnostics.Select(x => x.Message)));
                Assert.NotEmpty(preview.Diagrams);
                Assert.NotEmpty(preview.SharedLuaSources["config/ipia-hardware.json"]);
                Assert.Contains("config/ipia-shared-assets-provenance.json", preview.SharedLuaSources.Keys);
                Assert.NotEmpty(preview.Import.Provenance);
            }
            finally { DeleteFixture(root); }
        }
        /// <summary>Selection must point at a repository root rather than a nested directory.</summary>
        [Fact]
        public async Task Rejects_non_repository_folder()
        {
            var path = Path.Combine(Path.GetTempPath(), "DcsIpiaWorkspace-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(path);
                await Assert.ThrowsAsync<InvalidDataException>(() => new ModuleWorkspaceLoader().LoadAsync(path, path));
            }
            finally
            {
                DeleteFixture(path);
            }
        }

        /// <summary>Saved modifier defaults are diagnosed until committed, then survive import, SQLite and output.</summary>
        [Fact]
        public async Task Keyboard_defaults_require_committed_reimport_and_survive_workspace_reopen()
        {
            var root = Path.Combine(Path.GetTempPath(), "DcsIpiaModifiers-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Huey");
                foreach (var source in Directory.GetFiles(fixtures, "*", SearchOption.AllDirectories))
                    Copy(source, root, Path.GetRelativePath(fixtures, source));
                Commit(root);
                var loader = new ModuleWorkspaceLoader();
                const string modifierSourcePath = "src/Config/Input/UH-1H/modifiers.lua";
                var modifierPath = Path.Combine(root, modifierSourcePath);
                var original = File.ReadAllText(modifierPath);
                string[] keys = ["LAlt", "LCtrl", "LShift", "LWin", "RAlt", "RCtrl", "RShift", "RWin"];
                var definitions = string.Join("\n", keys.Select(key =>
                    $"[\"{key}\"] = {{ [\"device\"] = \"Keyboard\", [\"key\"] = \"{key}\", [\"switch\"] = false }},"));
                File.WriteAllText(modifierPath, original.Replace("local modifiers = {", "local modifiers = {\n" + definitions));
                Git(root, "add", modifierSourcePath);
                var extraPath = "src/Config/Input/UH-1H/joystick/new-device.diff.lua";
                File.WriteAllText(Path.Combine(root, extraPath), "local diff = {}\nreturn diff\n");
                File.WriteAllText(Path.Combine(root, "unrelated.txt"), "not an input");
                var stale = await loader.LoadAsync(BundledSharedCatalog.Root, root);
                Assert.True(stale.Import.Succeeded);
                Assert.DoesNotContain(stale.Import.Configuration!.Modifiers, x => x.Device == "Keyboard");
                Assert.Equal(new[] { extraPath, modifierSourcePath }.Order(StringComparer.Ordinal), stale.UncommittedSourcePaths);
                Assert.Contains(stale.Import.Diagnostics, x => x.Code == "uncommitted-module-sources" &&
                    !x.IsError && x.Message.Contains(modifierSourcePath, StringComparison.Ordinal));

                File.Delete(Path.Combine(root, extraPath));
                File.Delete(Path.Combine(root, "unrelated.txt"));
                Commit(root);
                var refreshed = await loader.LoadAsync(BundledSharedCatalog.Root, root);
                Assert.True(refreshed.Import.Succeeded, string.Join("; ", refreshed.Import.Diagnostics.Select(x => x.Message)));
                Assert.NotEqual(stale.ModuleCommit, refreshed.ModuleCommit);
                Assert.Empty(refreshed.UncommittedSourcePaths);
                Assert.DoesNotContain(refreshed.Import.Diagnostics, x => x.Code == "uncommitted-module-sources");
                Assert.Equal(9, refreshed.Import.Configuration!.Modifiers.Count);
                var snapshot = new EditorWorkspaceSnapshot(refreshed.Import.Configuration, refreshed.Import.Pages,
                    refreshed.Images, refreshed.Diagrams, [], refreshed.UiCommands, [],
                    KneeboardLabelImporter.Import(refreshed.Import.Metadata, refreshed.Import.Configuration),
                    refreshed.Import.Sources.Concat(refreshed.SharedLuaSources).ToDictionary(x => x.Key, x => x.Value));
                var database = "Data Source=" + Path.Combine(root, "workspace.db") + ";Pooling=False";
                await new SqliteMigrationRunner(database).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(database);
                await repository.SaveAsync(snapshot);
                var reopened = await repository.GetAsync(snapshot.Configuration.Module.Id);
                Assert.NotNull(reopened);
                var managed = WorkspaceStructureEditor.Describe(reopened).Modifiers
                    .Where(x => x.Layer == AssignmentLayer.Module).ToArray();
                foreach (var key in keys)
                {
                    var modifier = Assert.Single(managed, x => x.Name == key);
                    Assert.True(WorkspaceStructureEditor.IsDefaultKeyboardModifier(modifier));
                    Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.RemoveModifier(reopened, modifier));
                }
                var payload = SnapshotLuaPayloadRenderer.Generate(reopened);
                var output = DcsLuaParser.ParseModifiers(System.Text.Encoding.UTF8.GetString(payload["Config/Input/UH-1H/modifiers.lua"]));
                Assert.Equal(keys.Order(StringComparer.Ordinal),
                    output.Where(x => x.Device == "Keyboard").Select(x => x.Name).Order(StringComparer.Ordinal));
            }
            finally { DeleteFixture(root); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static readonly string[] AdfCategory = ["ADF Set Control panel"];
        private static readonly string[] ViewCategory = ["View"];
        private static void DeleteFixture(string path)
        {
            if (!Directory.Exists(path))
                return;
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(path, true);
        }
        private static void Copy(string source, string root, string path)
        {
            var target = Path.Combine(root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target);
        }
        private static void Commit(string root)
        {
            Git(root, "init", "-q");
            Git(root, "add", "--all");
            Git(root, "-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid", "commit", "-qm",
                "fixture");
        }
        private static void Git(string root, params string[] args)
        {
            using var process = new Process();
            process.StartInfo = new("git") { WorkingDirectory = root, RedirectStandardOutput = true,
                                             RedirectStandardError = true, UseShellExecute = false };
            foreach (var argument in args)
                process.StartInfo.ArgumentList.Add(argument);
            process.Start();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, error);
        }
        #endregion END private helpers
    }
}
