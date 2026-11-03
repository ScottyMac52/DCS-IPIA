using System.Text.Json;
using Heimdall.Domain.MultiSim;
using Heimdall.WarThunder;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Exercises portable ownership, native catalog evidence, resolution and atomic export diagnostics.</summary>
    public sealed class WarThunderBackendTests
    {
        #region BEGIN Public
        /// <summary>Both newline styles round-trip exactly through portable JSON, including unknown fields and repeated alternatives.</summary>
        [Theory]
        [InlineData("\n")]
        [InlineData("\r\n")]
        public void Native_capture_round_trips_through_the_portable_envelope(string newline)
        {
            var backend = new WarThunderProfileBackend();
            var source = WarThunderBackendFixture.Source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", newline, StringComparison.Ordinal);
            var profile = WarThunderBackendFixture.Import(backend, source);
            var reopened = JsonSerializer.Deserialize<ProfileDocument>(JsonSerializer.Serialize(profile))!;
            Assert.Equal(source, WarThunderBackendFixture.Export(backend, reopened));
            Assert.Equal("Test controls.blk", Assert.Single(backend.Export(reopened).Files).Key);
            Assert.Equal(2, profile.Devices.Count);
            Assert.Equal(8, WarThunderProfileBackend.ReadBindings(profile).Count);
            Assert.All(profile.Devices, device => { Assert.Empty(device.Axes); Assert.Empty(device.Buttons); Assert.Null(device.Identity!.InstanceId); });
            Assert.Empty(backend.Capabilities.ButtonModes);
            Assert.False(backend.Capabilities.ModifierChords || backend.Capabilities.NativeAxisFilters || backend.Capabilities.DeploysToGame);
            Assert.Contains(backend.Export(profile).Diagnostics, diagnostic => diagnostic.Code == "war-thunder.catalog.inherited" && !diagnostic.IsError);
            var inspection = WarThunderProfileBackend.Inspect(profile);
            Assert.Equal("war-thunder-native-inspection-v1", inspection.GetProperty("format").GetString());
            Assert.Equal(2, inspection.GetProperty("devices").GetArrayLength());
            Assert.Equal(8, inspection.GetProperty("bindings").GetArrayLength());
            Assert.NotEmpty(inspection.GetProperty("scalars").EnumerateArray());
        }
        /// <summary>Known, unknown and explicit empty commands share one context-scoped catalog with captured parameter types.</summary>
        [Fact]
        public void Catalog_is_captured_metadata_instead_of_a_per_command_encoder()
        {
            var backend = new WarThunderProfileBackend(); var profile = WarThunderBackendFixture.Import(backend);
            var catalog = backend.GetCatalog(profile);
            Assert.Equal(7, catalog.Commands.Count);
            var empty = catalog.Commands.Single(command => command.Metadata["war-thunder.command"] == "ID_EMPTY");
            Assert.Equal(empty.Id, catalog.Find(empty.Id)!.Id); Assert.Null(catalog.Find("foreign-context/hotkeys/ID_EMPTY"));
            Assert.Equal(CommandType.Button, empty.Type);
            var pitch = catalog.Commands.Single(command => command.Metadata["war-thunder.command"] == "pitch");
            Assert.Equal(CommandType.Axis, pitch.Type);
            using var parameters = JsonDocument.Parse(pitch.Metadata["war-thunder.parameter-types"]);
            Assert.Equal("r", parameters.RootElement.GetProperty("futureParam")[0].GetString());
            Assert.Contains(catalog.Commands, command => command.Metadata["war-thunder.command"] == "ID_FUTURE");
        }
        /// <summary>Device ranges are half-open native index spaces, independent of names, product equality and Windows instance order.</summary>
        [Theory]
        [InlineData(CommandType.Button, 0, 0)]
        [InlineData(CommandType.Button, 3, 0)]
        [InlineData(CommandType.Button, 4, 1)]
        [InlineData(CommandType.Button, 7, 1)]
        [InlineData(CommandType.Axis, 0, 0)]
        [InlineData(CommandType.Axis, 2, 1)]
        [InlineData(CommandType.Axis, 3, 1)]
        public void Native_offsets_resolve_to_saved_mapping_occurrences(CommandType type, int index, int device)
        {
            var backend = new WarThunderProfileBackend(); var profile = WarThunderBackendFixture.Import(backend);
            Assert.Equal("war-thunder.joystick." + device, Assert.Single(WarThunderProfileBackend.ResolveNativeInput(profile, type, index)).Id);
            Assert.Empty(WarThunderProfileBackend.ResolveNativeInput(profile, CommandType.Button, 8));
            Assert.Empty(WarThunderProfileBackend.ResolveNativeInput(profile, CommandType.Axis, 4));
            Assert.Empty(WarThunderProfileBackend.ResolveNativeInput(profile, type, -1));
            Assert.Empty(WarThunderProfileBackend.ResolveNativeInput(profile, CommandType.Hat, 0));
        }
        /// <summary>Physical identity resolution requires an explicit instance, including repeated identical units.</summary>
        [Fact]
        public void Saved_products_never_automatically_link_a_physical_controller()
        {
            var backend = new WarThunderProfileBackend(); var profile = WarThunderBackendFixture.Import(backend);
            var reference = profile.Devices[0].Identity!;
            var first = new PhysicalDeviceIdentity { WindowsName = reference.WindowsName, VendorId = 1, ProductId = 2, InstanceId = "unit-A" };
            var second = new PhysicalDeviceIdentity { WindowsName = reference.WindowsName, VendorId = 1, ProductId = 2, InstanceId = "unit-B" };
            Assert.Equal("unresolved", backend.ResolveDevice(reference, [first, second]).Status);
            Assert.Equal("disconnected", backend.ResolveDevice(first, [second]).Status);
            Assert.Equal("resolved", backend.ResolveDevice(first, [second, first]).Status);
            Assert.Equal("ambiguous", backend.ResolveDevice(first, [first, first]).Status);
        }
        /// <summary>Unsupported envelopes and out-of-band portable mutations cannot silently export stale native text.</summary>
        [Theory]
        [InlineData("schema")]
        [InlineData("backend")]
        [InlineData("version")]
        [InlineData("context")]
        [InlineData("identity")]
        [InlineData("metadata")]
        [InlineData("device")]
        [InlineData("binding")]
        [InlineData("native")]
        [InlineData("source-text")]
        public void Portable_mutations_return_no_partial_output(string mutation)
        {
            var backend = new WarThunderProfileBackend(); var profile = WarThunderBackendFixture.Import(backend);
            switch (mutation)
            {
                case "schema": profile.SchemaVersion = 2; break;
                case "backend": profile.BackendId = "elite-dangerous"; break;
                case "version": profile.BackendVersion = 2; break;
                case "context": profile.ContextId = "other"; break;
                case "identity": profile.ProfileId = "other"; break;
                case "metadata": profile.Metadata["unknown"] = "change"; break;
                case "device": profile.Devices[0].Identity!.InstanceId = "invented"; break;
                case "binding": profile.Devices[0].Buttons.Add(new() { SourceButton = "JOY_BTN1", CommandId = "invented" }); break;
                case "native": profile.NativeData["unknown"] = JsonSerializer.SerializeToElement(1); break;
                case "source-text":
                    var evidence = JsonSerializer.Deserialize<Dictionary<string, string>>(profile.NativeData["war-thunder.v1"].GetRawText())!;
                    evidence["Text"] = evidence["Text"].Replace("joyButton:i=1", "joyButton:i=3", StringComparison.Ordinal);
                    profile.NativeData["war-thunder.v1"] = JsonSerializer.SerializeToElement(evidence);
                    break;
            }
            var plan = backend.Export(profile);
            Assert.False(plan.Succeeded); Assert.Empty(plan.Files);
            Assert.Contains(plan.Diagnostics, diagnostic => diagnostic.IsError && diagnostic.Code == "war-thunder.export.blocked");
        }
        /// <summary>Import boundaries reject unsafe filenames, multiple envelopes, unsupported versions and cancelled work.</summary>
        [Theory]
        [InlineData("../capture.blk")]
        [InlineData("C:\\capture.blk")]
        [InlineData("capture.txt")]
        [InlineData(".blk")]
        public void Sources_require_a_safe_single_exported_filename(string fileName)
        {
            var backend = new WarThunderProfileBackend();
            Assert.Throws<InvalidOperationException>(() => backend.Import("context", new Dictionary<string, string> { [fileName] = WarThunderBackendFixture.Source }));
        }
        /// <summary>Foreign versions and ambiguous envelopes never become partially imported profiles.</summary>
        [Fact]
        public void Invalid_versions_sources_and_cancellation_are_rejected()
        {
            var backend = new WarThunderProfileBackend();
            Assert.Throws<InvalidOperationException>(() => WarThunderBackendFixture.Import(backend, "controls{version:i=6;}"));
            Assert.Throws<InvalidOperationException>(() => WarThunderBackendFixture.Import(backend, "controls{}"));
            Assert.Throws<InvalidOperationException>(() => WarThunderBackendFixture.Import(backend, "controls{version:i=5;}controls{version:i=5;}"));
            Assert.Throws<InvalidOperationException>(() => backend.Import("context", new Dictionary<string, string>()));
            Assert.Throws<OperationCanceledException>(() => backend.Import("context", new Dictionary<string, string>(), new CancellationToken(true)));
            Assert.Throws<OperationCanceledException>(() => backend.Export(WarThunderBackendFixture.Import(backend), new CancellationToken(true)));
            Assert.False(backend.Export(null!).Succeeded);
        }
        /// <summary>Unknown typed scalars remain lossless, catalogued and explicitly export-only.</summary>
        [Fact]
        public void Opaque_native_data_is_retained_with_nonblocking_diagnostics()
        {
            var backend = new WarThunderProfileBackend();
            var source = WarThunderBackendFixture.Source.Replace("futureParam:r=2", "futureParam:q=opaque", StringComparison.Ordinal);
            var profile = WarThunderBackendFixture.Import(backend, source);
            Assert.Equal(source, WarThunderBackendFixture.Export(backend, profile));
            Assert.Contains(backend.Export(profile).Diagnostics, diagnostic => diagnostic.Code == "war-thunder.syntax.opaque" && !diagnostic.IsError);
            var record = WarThunderBackendFixture.Binding(profile, "pitch");
            Assert.False(WarThunderProfileBackend.GetBindingCapabilities(profile, record.Id).RebindNativeInputs);
            Assert.Throws<InvalidOperationException>(() => WarThunderProfileBackend.EditAxisParameter(profile, record.Id, WarThunderBackendFixture.Field(record, "kMul").Address, "2"));
        }
        /// <summary>Empty native exports retain no fabricated devices or assignments and do not claim a full inherited catalog.</summary>
        [Fact]
        public void A_captured_empty_profile_remains_empty()
        {
            var backend = new WarThunderProfileBackend(); const string source = "controls{version:i=5;}";
            var profile = WarThunderBackendFixture.Import(backend, source);
            Assert.Empty(profile.Devices); Assert.Empty(WarThunderProfileBackend.ReadBindings(profile)); Assert.Empty(backend.GetCatalog(profile).Commands);
            Assert.Equal(source, WarThunderBackendFixture.Export(backend, profile));
        }
        #endregion END Public
    }
}
