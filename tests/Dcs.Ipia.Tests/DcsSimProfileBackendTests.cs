using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain.MultiSim;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Exercises the portable backend through actual DCS files, edits and serialization.</summary>
    public sealed class DcsSimProfileBackendTests
    {
        #region BEGIN Public
        /// <summary>Real native profiles survive the generic contract and JSON boundary byte for byte.</summary>
        [Fact]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859", Justification = "This test deliberately exercises the backend through the shared application contract.")]
        public void Native_fixture_round_trip_preserves_sources_and_contexts()
        {
            ISimProfileBackend backend = new DcsSimProfileBackend();
            var files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "F4U", "src"), "*.lua", SearchOption.AllDirectories)
                .ToDictionary(path => Path.GetRelativePath(Path.Combine(AppContext.BaseDirectory, "Fixtures", "F4U", "src"), path).Replace('\\', '/'), File.ReadAllText);
            var profile = backend.Import("F4U-1D", files);
            profile = JsonSerializer.Deserialize<ProfileDocument>(JsonSerializer.Serialize(profile))!;
            var plan = backend.Export(profile);
            Assert.True(plan.Succeeded, string.Join("; ", plan.Diagnostics.Select(x => x.Message)));
            Assert.Equal(files.OrderBy(x => x.Key), plan.Files.OrderBy(x => x.Key));
            Assert.Equal("dcs", profile.BackendId);
            Assert.NotEmpty(backend.GetCatalog(profile).Commands);
        }
        /// <summary>Scoped catalogs distinguish identical native command IDs and return independent command objects.</summary>
        [Fact]
        public void Catalogs_and_capabilities_are_explicit_and_independent()
        {
            var backend = new DcsSimProfileBackend();
            var profile = backend.Import("Test", Sources());
            var catalog = backend.GetCatalog(profile);
            var commands = catalog.Commands;
            Assert.Equal("dcs", backend.SimId);
            Assert.Equal("dcs", catalog.SimId);
            Assert.True(backend.Capabilities.ModifierChords);
            Assert.True(backend.Capabilities.NativeAxisFilters);
            Assert.False(backend.Capabilities.DeploysToGame);
            Assert.Equal(1, backend.Capabilities.Version);
            Assert.Equal(BindingMode.Press, Assert.Single(backend.Capabilities.ButtonModes));
            var module = commands.Single(x => x.Context == "Test" && x.Metadata["nativeId"] == "one");
            var ui = commands.Single(x => x.Context == "UiLayer" && x.Metadata["nativeId"] == "one");
            Assert.NotEqual(module.Id, ui.Id);
            Assert.True(module.IsModuleSpecific);
            Assert.False(ui.IsModuleSpecific);
            module.DisplayName = "changed outside catalog";
            Assert.Equal("One", catalog.Find(module.Id)!.DisplayName);
            Assert.Null(catalog.Find("one"));
        }

        /// <summary>Edits survive JSON, preserve exact sparse native filters, and retain removal and changed history.</summary>
        [Fact]
        public void Native_edits_preserve_chords_sparse_filters_and_axis_removals()
        {
            var backend = new DcsSimProfileBackend();
            var profile = backend.Import("Test", Sources());
            profile = JsonSerializer.Deserialize<ProfileDocument>(JsonSerializer.Serialize(profile))!;
            var device = profile.Devices.Single(x => x.ContextId == "Test");
            device.Buttons[0].SourceButton = "JOY_BTN3";
            device.Buttons[0].Modifiers.Add(new() { Source = "Toggle" });
            device.Axes[0].Modifiers.Add(new() { Source = "Hold" });
            device.Axes[0].Deadzone = 12.5f;
            device.Axes[0].Invert = true;
            var plan = backend.Export(profile);
            Assert.True(plan.Succeeded, string.Join("; ", plan.Diagnostics.Select(x => x.Message)));
            var parsed = DcsLuaParser.ParseProfile(plan.Files[device.LogicalId], Guid.NewGuid());
            Assert.Contains(parsed, x => x.InputKey == "JOY_BTN3" && x.Reformers.Count == 1 && x.Reformers[0] == "Toggle");
            Assert.Contains(parsed, x => x.CommandId == "two" && x.Disposition == global::Dcs.Ipia.Domain.BindingDisposition.Changed);
            var axis = Assert.Single(parsed, x => x.Section == global::Dcs.Ipia.Domain.BindingSection.Axis && x.Disposition != global::Dcs.Ipia.Domain.BindingDisposition.Removed);
            Assert.Equal(12.5, axis.Filter!.Deadzone);
            Assert.True(axis.Filter.Invert);
            Assert.False(axis.Filter.Slider);
            Assert.Null(axis.Filter.SaturationX);
            Assert.Equal(20, Assert.Single(axis.Filter.Curvature!));
            Assert.Equal("Hold", Assert.Single(axis.Reformers));
            Assert.Contains(parsed, x => x.CommandId == "old" && x.Disposition == global::Dcs.Ipia.Domain.BindingDisposition.Removed);
            device.Axes.Clear();
            parsed = DcsLuaParser.ParseProfile(backend.Export(profile).Files[device.LogicalId], Guid.NewGuid());
            Assert.Contains(parsed, x => x.CommandId == "roll" && x.Disposition == global::Dcs.Ipia.Domain.BindingDisposition.Removed);
        }

        /// <summary>Unknown native fields are preserved on untouched export and block lossy edited output.</summary>
        [Fact]
        public void Unknown_native_fields_are_preserved_or_blocked_before_export()
        {
            var sources = Sources();
            sources[ModulePath] = sources[ModulePath].Replace("local diff = {", "local diff = { [\"futureSetting\"] = { [1] = 17 },", StringComparison.Ordinal);
            var backend = new DcsSimProfileBackend();
            var profile = backend.Import("Test", sources);
            Assert.Equal(sources[ModulePath], backend.Export(profile).Files[ModulePath]);
            profile.Devices.Single(x => x.ContextId == "Test").Buttons.Clear();
            var plan = backend.Export(profile);
            Assert.False(plan.Succeeded);
            Assert.Empty(plan.Files);
            Assert.Contains("futureSetting", Assert.Single(plan.Diagnostics).Message);
        }

        /// <summary>Nonstandard source syntax blocks edits, while escaped command labels and normal comments remain supported.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Native_syntax_guard_prevents_silent_loss(bool nonstandard)
        {
            var sources = Sources();
            sources[ModulePath] = nonstandard
                ? sources[ModulePath].Replace("local diff = {", "local diff = { futureSetting = 17,", StringComparison.Ordinal)
                : sources[ModulePath].Replace("One", "One \\\"quoted\\\"", StringComparison.Ordinal) + "\n-- descriptive comment\n";
            var backend = new DcsSimProfileBackend();
            var profile = backend.Import("Test", sources);
            profile.Devices.Single(x => x.ContextId == "Test").Buttons[0].SourceButton = "JOY_BTN3";
            var plan = backend.Export(profile);
            Assert.Equal(!nonstandard, plan.Succeeded);
            if (nonstandard) Assert.Empty(plan.Files);
        }

        /// <summary>Unknown nested fields, numeric root data and mistyped native tuning cannot be discarded on edit.</summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void Native_rewrite_requires_a_recognized_field_shape(int scenario)
        {
            var sources = Sources();
            sources[ModulePath] = scenario switch
            {
                0 => Native.Replace("local diff = {", "local diff = { [1] = 17,", StringComparison.Ordinal),
                1 => Native.Replace("[\"deadzone\"] = 1", "[\"deadzone\"] = \"future\"", StringComparison.Ordinal),
                2 => Native.Replace("[\"deadzone\"] = 1", "[\"deadzone\"] = 1, [\"key\"] = \"future\"", StringComparison.Ordinal),
                4 => Native.Replace("[1] = 20", "[3] = 20", StringComparison.Ordinal),
                _ => Native.Replace("[\"deadzone\"] = 1", "[\"deadzone\"] = 1, [1] = 17", StringComparison.Ordinal)
            };
            var backend = new DcsSimProfileBackend();
            var profile = backend.Import("Test", sources);
            Assert.Equal(sources[ModulePath], backend.Export(profile).Files[ModulePath]);
            profile.Devices.Single(x => x.ContextId == "Test").Buttons[0].SourceButton = "JOY_BTN3";
            var plan = backend.Export(profile);
            Assert.False(plan.Succeeded);
            Assert.Empty(plan.Files);
        }

        /// <summary>Invalid documents and unsupported edits return diagnostics without publishing partial native files.</summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(10)]
        [InlineData(11)]
        [InlineData(12)]
        [InlineData(13)]
        [InlineData(14)]
        [InlineData(15)]
        [InlineData(16)]
        [InlineData(17)]
        [InlineData(18)]
        [InlineData(19)]
        [InlineData(20)]
        [InlineData(21)]
        [InlineData(22)]
        [InlineData(23)]
        [InlineData(24)]
        [InlineData(25)]
        [InlineData(26)]
        [InlineData(27)]
        [InlineData(28)]
        public void Invalid_conversion_is_atomic(int scenario)
        {
            var backend = new DcsSimProfileBackend();
            var profile = backend.Import("Test", Sources());
            var device = profile.Devices.Single(x => x.ContextId == "Test");
            switch (scenario)
            {
                case 0: profile.SchemaVersion = 99; break;
                case 1: profile.BackendVersion = 99; break;
                case 2: profile.BackendId = "other-game"; break;
                case 3: profile.ContextId = "other-module"; break;
                case 4: profile.ProfileId = "other-profile"; break;
                case 5: device.Identity!.InstanceId = Guid.NewGuid().ToString(); break;
                case 6: device.ContextId = "UiLayer"; break;
                case 7: device.LogicalId = "../escape"; break;
                case 8: profile.Devices.RemoveAt(0); break;
                case 9: profile.Devices[1] = profile.Devices[0]; break;
                case 10: profile.NativeData.Clear(); break;
                case 11: device.Buttons[0].Mode = BindingMode.Toggle; break;
                case 12: device.Buttons[0].Modifiers.Add(new() { Source = "unknown" }); break;
                case 13: device.Buttons[0].Modifiers.Add(new() { Source = "Hold", Value = "0.5" }); break;
                case 14: device.Buttons[0].CommandId = "one"; break;
                case 15: device.Axes[0].Sensitivity = 2; break;
                case 16: device.Axes[0].Deadzone = float.NaN; break;
                case 17: device.Axes[0].Deadzone = 101; break;
                case 18: device.Axes[0].Curve = CurveType.Exponential; break;
                case 19: device.Buttons[0].SimSpecific["unknown"] = true; break;
                case 20: device.Axes[0].SimSpecific["unknown"] = true; break;
                case 21: device.Buttons.Add(device.Buttons[0]); break;
                case 22: device.Buttons = null!; break;
                case 23: device.Axes[0].SimSpecific["dcs.filter"] = JsonSerializer.SerializeToElement(new { Deadzone = 500 }); break;
                case 24: device.Axes[0].SimSpecific["dcs.filter"] = JsonSerializer.SerializeToElement(new { future = true }); break;
                case 25: profile.NativeData["dcs.v1"] = JsonSerializer.SerializeToElement(new { wrongShape = true }); break;
                case 26: device.Buttons[0].CommandId = profile.Devices.Single(x => x.ContextId == "UiLayer").Buttons[0].CommandId; break;
                case 27: device.Buttons[0].CommandId = device.Axes[0].CommandId; break;
                case 28: device.Buttons[0].Modifiers.AddRange([new() { Source = "Hold" }, new() { Source = "Hold" }]); break;
            }
            var plan = backend.Export(profile);
            Assert.False(plan.Succeeded);
            Assert.Empty(plan.Files);
            Assert.Equal("dcs-conversion-blocked", Assert.Single(plan.Diagnostics).Code);
        }

        /// <summary>Input paths are portable, context-owned and traversal-free on either host operating system.</summary>
        [Theory]
        [InlineData("../escape.diff.lua")]
        [InlineData("Config/Input/Other/joystick/Stick.diff.lua")]
        [InlineData("Config/Input/Test/joystick/../Stick.diff.lua")]
        [InlineData("C:/Config/Input/Test/joystick/Stick.diff.lua")]
        [InlineData("Config/Input/Test/joystick/Stick.txt")]
        public void Unsafe_sources_are_rejected(string path)
        {
            Assert.Throws<InvalidDataException>(() => new DcsSimProfileBackend().Import("Test", new Dictionary<string, string> { [path] = Native }));
        }

        /// <summary>Missing and duplicate instance evidence never falls back to device names or vendor/product IDs.</summary>
        [Theory]
        [InlineData(0, "disconnected")]
        [InlineData(1, "resolved")]
        [InlineData(2, "ambiguous")]
        public void Repeated_controller_resolution_requires_exact_instance_evidence(int count, string expected)
        {
            var backend = new DcsSimProfileBackend();
            var identity = new PhysicalDeviceIdentity { InstanceId = "51fa39b0-cb32-11ed-8008-444553540000", WindowsName = "MFD", VendorId = 1103, ProductId = 1 };
            var attached = Enumerable.Range(0, count).Select(_ => new PhysicalDeviceIdentity { InstanceId = identity.InstanceId!.ToUpperInvariant(), WindowsName = "Renamed MFD" }).ToArray();
            var result = backend.ResolveDevice(identity, attached);
            Assert.Equal(expected, result.Status);
            Assert.Equal(count, result.Matches.Count);
            identity.InstanceId = null;
            Assert.Equal("unresolved", backend.ResolveDevice(identity, attached).Status);
        }

        /// <summary>Cancelled operations cannot return a partial profile or export plan.</summary>
        [Fact]
        public void Cancellation_and_missing_native_evidence_are_explicit()
        {
            var backend = new DcsSimProfileBackend();
            var token = new CancellationToken(true);
            Assert.Throws<OperationCanceledException>(() => backend.Import("Test", Sources(), token));
            var profile = backend.Import("Test", Sources());
            Assert.Throws<OperationCanceledException>(() => backend.Export(profile, token));
            Assert.Throws<OperationCanceledException>(() => DcsSimProfileBackend.Restore(profile, token));
            Assert.Throws<InvalidDataException>(() => DcsSimProfileBackend.Restore(profile));
            Assert.Throws<InvalidDataException>(() => backend.Import("..", Sources()));
            Assert.Throws<InvalidDataException>(() => backend.Import("Test", new Dictionary<string, string>()));
        }
        #endregion END Public
        #region BEGIN private helpers
        private const string ModulePath = "Config/Input/Test/joystick/Stick {51FA39B0-CB32-11ed-8008-444553540000}.diff.lua";
        private const string Native = """
            local diff = {
              ["keyDiffs"] = {
                ["one"] = { ["added"] = { [1] = { ["key"] = "JOY_BTN1" } }, ["name"] = "One" },
                ["two"] = { ["changed"] = { [1] = { ["key"] = "JOY_BTN2" } }, ["name"] = "Two" }
              },
              ["axisDiffs"] = {
                ["roll"] = { ["added"] = { [1] = { ["key"] = "JOY_X", ["filter"] = { ["deadzone"] = 1, ["curvature"] = { [1] = 20 }, ["slider"] = false } } }, ["name"] = "Roll" },
                ["old"] = { ["removed"] = { [1] = { ["key"] = "JOY_Z" } }, ["name"] = "Old" }
              }
            }
            return diff
            """;
        private static Dictionary<string, string> Sources() => new()
        {
            [ModulePath] = Native,
            [ModulePath.Replace("/Test/", "/UiLayer/", StringComparison.Ordinal)] = Native,
            ["Config/Input/Test/modifiers.lua"] = Modifiers,
            ["Config/Input/UiLayer/modifiers.lua"] = Modifiers
        };
        private const string Modifiers = """
            local modifiers = {
              ["Hold"] = { ["device"] = "Stick", ["key"] = "JOY_BTN8", ["switch"] = false },
              ["Toggle"] = { ["device"] = "Stick", ["key"] = "JOY_BTN9", ["switch"] = true }
            }
            return modifiers
            """;
        #endregion END private helpers
    }
}
