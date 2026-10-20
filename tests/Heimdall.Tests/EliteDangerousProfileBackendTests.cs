using System.Text.Json;
using System.Xml.Linq;
using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;
using Heimdall.EliteDangerous;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Tests captured Elite XML through the shared backend and persisted portable profile boundary.</summary>
    public sealed class EliteDangerousProfileBackendTests
    {
        #region BEGIN Public
        /// <summary>A synthetic 4.2 file preserves every byte across import, JSON persistence and export.</summary>
        [Fact]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859", Justification = "Exercises the shared application interface.")]
        public void Synthetic_42_fixture_round_trips_through_shared_contract()
        {
            ISimProfileBackend backend = new EliteDangerousProfileBackend();
            var files = Fixture();
            var profile = Persist(backend.Import("controls", files));
            var plan = backend.Export(profile);
            Assert.True(plan.Succeeded, string.Join("; ", plan.Diagnostics.Select(x => x.Message)));
            Assert.Equal(files.OrderBy(x => x.Key), plan.Files.OrderBy(x => x.Key));
            Assert.Equal("Synthetic", profile.Name);
            Assert.Equal("Synthetic.4.2.binds", profile.ProfileId);
            Assert.Equal(3, profile.Devices.Count);
            Assert.Equal(5, backend.GetCatalog(profile).Commands.Count);
            Assert.Contains(profile.Devices, x => x.Identity!.SimSpecificIds.GetValueOrDefault("ed.deviceIndex") == "0");
            Assert.All(profile.Devices, x => Assert.Null(x.Identity!.InstanceId));
        }

        /// <summary>Catalogs include empty slots, remain scoped and cannot be mutated through returned commands.</summary>
        [Fact]
        public void Catalog_and_capabilities_are_independent_and_explicit()
        {
            var backend = new EliteDangerousProfileBackend();
            var profile = backend.Import("ship", Sources());
            var catalog = backend.GetCatalog(profile);
            Assert.Equal("elite-dangerous", backend.SimId);
            Assert.Equal(backend.SimId, catalog.SimId);
            Assert.Equal(1, backend.Capabilities.Version);
            Assert.Equal(new[] { BindingMode.Press, BindingMode.Hold }, backend.Capabilities.ButtonModes);
            Assert.True(backend.Capabilities.ModifierChords);
            Assert.True(backend.Capabilities.NativeAxisFilters);
            Assert.False(backend.Capabilities.DeploysToGame);
            var fire = catalog.Commands.Single(x => x.DisplayName == "Fire");
            Assert.Equal(CommandType.Button, fire.Type);
            Assert.Equal("1", fire.Metadata["ed.toggleOn"]);
            fire.Metadata.Clear();
            fire.DisplayName = "tampered";
            Assert.Equal("Fire", catalog.Find(fire.Id)!.DisplayName);
            Assert.Null(catalog.Find("Fire"));
            Assert.NotEqual(fire.Id, backend.GetCatalog(backend.Import("srv", Sources())).Commands[0].Id);
            Assert.Equal(CommandType.Axis, catalog.Commands.Single(x => x.DisplayName == "Pitch").Type);
        }

        /// <summary>Slot edits preserve secondary assignments, cross-device chords, settings and selector evidence.</summary>
        [Fact]
        public void Edits_preserve_unrelated_data_and_survive_reimport()
        {
            var backend = new EliteDangerousProfileBackend();
            var profile = Persist(backend.Import("controls", Sources()));
            var stick = profile.Devices.Single(x => x.Identity!.SimSpecificIds["ed.device"] == "ExampleStick");
            stick.Buttons[0].SourceButton = "Joy_9";
            stick.Buttons[0].Mode = BindingMode.Hold;
            stick.Buttons[0].Modifiers.Add(new() { Source = profile.Devices.Single(x => x.Identity!.SimSpecificIds["ed.device"] == "Keyboard").LogicalId, Value = "Key_LeftShift" });
            stick.Axes[0].Deadzone = 12.5f;
            stick.Axes[0].Invert = false;
            profile.Metadata[profile.Metadata.Keys.Single()] = "0";
            var plan = backend.Export(Persist(profile));
            Assert.True(plan.Succeeded, string.Join("; ", plan.Diagnostics.Select(x => x.Message)));
            var xml = XDocument.Parse(plan.Files["Synthetic.4.2.binds"]);
            Assert.Equal("Joy_9", (string?)xml.Root!.Element("Fire")!.Element("Primary")!.Attribute("Key"));
            Assert.Equal("Key_Space", (string?)xml.Root.Element("Fire")!.Element("Secondary")!.Attribute("Key"));
            Assert.Equal("0.125", (string?)xml.Root.Element("Pitch")!.Element("Deadzone")!.Attribute("Value"));
            Assert.Equal("keep", (string?)xml.Root.Element("FutureSetting")!.Attribute("Value"));
            Assert.Equal(2, xml.Root.Elements("MouseGUI").Count());
            Assert.Equal(Sources()["StartPreset.4.start"], plan.Files["StartPreset.4.start"]);
            var restored = backend.Import("controls", plan.Files);
            var restoredStick = restored.Devices.Single(x => x.LogicalId == stick.LogicalId);
            Assert.Equal(12.5f, restoredStick.Axes[0].Deadzone);
            Assert.False(restoredStick.Axes[0].Invert);
            Assert.Equal(BindingMode.Hold, restoredStick.Buttons[0].Mode);
            Assert.Equal("Key_LeftShift", Assert.Single(restoredStick.Buttons[0].Modifiers).Value);
            Assert.Equal("0", restored.Metadata.Values.Single());
        }

        /// <summary>Assignments can be cleared, moved between captured devices and added to unbound native slots.</summary>
        [Fact]
        public void Clear_move_and_assign_unbound_slots()
        {
            var backend = new EliteDangerousProfileBackend();
            var profile = backend.Import("controls", Sources());
            var stick = profile.Devices.Single(x => x.Axes.Count != 0);
            var keyboard = profile.Devices.Single(x => x.Identity!.SimSpecificIds["ed.device"] == "Keyboard");
            stick.Axes.Clear();
            var fire = stick.Buttons.Single();
            stick.Buttons.Clear();
            fire.SourceButton = "Key_F";
            fire.Mode = BindingMode.Press;
            keyboard.Buttons.Add(fire);
            keyboard.Buttons.Add(new()
            {
                CommandId = backend.GetCatalog(profile).Commands.Single(x => x.DisplayName == "Empty").Id,
                SourceButton = "Key_E", SimSpecific = new() { ["ed.slot"] = "Secondary" }
            });
            var plan = backend.Export(profile);
            Assert.True(plan.Succeeded);
            var xml = XDocument.Parse(plan.Files["Synthetic.4.2.binds"]);
            Assert.Equal("{NoDevice}", (string?)xml.Root!.Element("Pitch")!.Element("Binding")!.Attribute("Device"));
            Assert.Equal("Keyboard", (string?)xml.Root.Element("Fire")!.Element("Primary")!.Attribute("Device"));
            Assert.Null(xml.Root.Element("Fire")!.Element("Primary")!.Element("Hold"));
            Assert.NotNull(backend.Import("controls", plan.Files));
        }

        /// <summary>A button edit leaves the exact original deadzone precision intact.</summary>
        [Fact]
        public void Native_precision_survives_axis_key_changes()
        {
            var backend = new EliteDangerousProfileBackend();
            var profile = backend.Import("controls", Sources());
            profile.Devices.Single(x => x.Axes.Count != 0).Axes[0].SourceAxis = "Joy_RZAxis";
            var xml = XDocument.Parse(backend.Export(profile).Files["Synthetic.4.2.binds"]);
            Assert.Equal("0.123456789123", (string?)xml.Root!.Element("Pitch")!.Element("Deadzone")!.Attribute("Value"));
        }

        /// <summary>Unsupported portable edits fail atomically with actionable diagnostics.</summary>
        [Theory]
        [InlineData("schema")]
        [InlineData("backend")]
        [InlineData("version")]
        [InlineData("context")]
        [InlineData("name")]
        [InlineData("profileId")]
        [InlineData("evidence")]
        [InlineData("evidenceNull")]
        [InlineData("contextEvidence")]
        [InlineData("nativeDataNull")]
        [InlineData("device")]
        [InlineData("identity")]
        [InlineData("deviceContext")]
        [InlineData("systemRemoval")]
        [InlineData("duplicateDevice")]
        [InlineData("buttonsNull")]
        [InlineData("axesNull")]
        [InlineData("devicesNull")]
        [InlineData("deviceNull")]
        [InlineData("buttonNull")]
        [InlineData("axisNull")]
        [InlineData("modifierNull")]
        [InlineData("metadataNull")]
        [InlineData("extensionsNull")]
        [InlineData("metadata")]
        [InlineData("metadataValue")]
        [InlineData("mode")]
        [InlineData("slot")]
        [InlineData("extension")]
        [InlineData("slotNull")]
        [InlineData("key")]
        [InlineData("duplicateSlot")]
        [InlineData("command")]
        [InlineData("modifierDevice")]
        [InlineData("modifierKey")]
        [InlineData("duplicateModifier")]
        [InlineData("modifiersNull")]
        [InlineData("curve")]
        [InlineData("sensitivity")]
        [InlineData("deadzoneNaN")]
        [InlineData("deadzoneLow")]
        [InlineData("deadzoneHigh")]
        [InlineData("axisSlot")]
        [InlineData("axisModifier")]
        public void Unsupported_edits_are_blocked(string change)
        {
            var backend = new EliteDangerousProfileBackend();
            var profile = backend.Import("controls", Sources());
            Mutate(profile, change);
            var plan = backend.Export(profile);
            Assert.False(plan.Succeeded);
            Assert.Empty(plan.Files);
            Assert.Contains(plan.Diagnostics, x => x.IsError && x.Code == "ed.export.blocked" && x.Message.Length != 0);
        }

        /// <summary>Malformed native documents fail at import before a portable profile is published.</summary>
        [Theory]
        [InlineData("<Root/>")]
        [InlineData("<Root")]
        [InlineData("<!DOCTYPE Root [<!ENTITY x 'x'>]><Root PresetName='A' MajorVersion='4' MinorVersion='2'>&x;</Root>")]
        [InlineData("<Root PresetName='A' MajorVersion='3' MinorVersion='0'/>")]
        [InlineData("<Root PresetName='A' MajorVersion='4' MinorVersion='x'/>")]
        [InlineData("<Root PresetName='' MajorVersion='4' MinorVersion='2'/>")]
        [InlineData("<Root PresetName='A' MajorVersion='4' MinorVersion='2'><x:Fire xmlns:x='urn:future'/></Root>")]
        public void Invalid_xml_is_rejected(string xml)
        {
            Assert.ThrowsAny<Exception>(() => new EliteDangerousProfileBackend().Import("controls", new Dictionary<string, string> { ["Synthetic.4.2.binds"] = xml }));
        }

        /// <summary>Invalid native assignment values are never silently approximated.</summary>
        [Theory]
        [InlineData("Device=\"ExampleStick\"", "Device=\"\"")]
        [InlineData("DeviceIndex=\"0\"", "DeviceIndex=\"-1\"")]
        [InlineData("Key=\"Joy_1\"", "Key=\"\"")]
        [InlineData("Key=\"\"", "Key=\"Joy_8\"")]
        [InlineData("Value=\"0.123456789123\"", "Value=\"NaN\"")]
        [InlineData("Value=\"0.123456789123\"", "Value=\"1.1\"")]
        [InlineData("Value=\"0.123456789123\"", "Value=\"-0.1\"")]
        [InlineData("<Inverted Value=\"1\" />", "")]
        [InlineData("<Inverted Value=\"1\" />", "<Inverted Value=\"2\" />")]
        [InlineData("<ToggleOn Value=\"1\" />", "<ToggleOn Value=\"1\" /><ToggleOn Value=\"0\" />")]
        [InlineData("<Hold Value=\"1\" />", "<Hold Value=\"2\" />")]
        [InlineData("<Empty>", "<Fire>")]
        [InlineData("</Empty>", "</Fire>")]
        public void Invalid_native_values_are_rejected(string from, string to)
        {
            var sources = Sources();
            sources["Synthetic.4.2.binds"] = sources["Synthetic.4.2.binds"].Replace(from, to, StringComparison.Ordinal);
            Assert.ThrowsAny<Exception>(() => new EliteDangerousProfileBackend().Import("controls", sources));
        }

        /// <summary>Unknown binding fields remain exact until an edit would discard them.</summary>
        [Theory]
        [InlineData(" Future='keep'", "")]
        [InlineData(" x:Key='keep'", "")]
        [InlineData("", "<Modifier Device='Keyboard' Key='Key_Shift' x:Key='keep'/>")]
        [InlineData("", "<Future Value='keep'/>")]
        [InlineData("", "<!--keep-->")]
        [InlineData("", "unknown text")]
        [InlineData("", "<Hold Value='1' Future='keep'/>")]
        [InlineData("", "<Modifier Device='Keyboard' Key='Key_Shift'><Future/></Modifier>")]
        public void Unknown_binding_data_is_preserved_or_blocks_editing(string attributes, string children)
        {
            var sources = Sources();
            sources["Synthetic.4.2.binds"] = sources["Synthetic.4.2.binds"].Replace("Key=\"Joy_1\">", "Key=\"Joy_1\"" + attributes + ">" + children, StringComparison.Ordinal);
            sources["Synthetic.4.2.binds"] = sources["Synthetic.4.2.binds"].Replace("<Root ", "<Root xmlns:x='urn:future' ", StringComparison.Ordinal);
            var backend = new EliteDangerousProfileBackend();
            var profile = backend.Import("controls", sources);
            Assert.Equal(sources["Synthetic.4.2.binds"], backend.Export(Persist(profile)).Files["Synthetic.4.2.binds"]);
            profile.Devices.Single(x => x.Buttons.Any(b => b.SourceButton == "Joy_1")).Buttons.Single().SourceButton = "Joy_20";
            Assert.False(backend.Export(profile).Succeeded);
        }

        /// <summary>Missing, excessive and unsafe native source paths are rejected.</summary>
        [Theory]
        [InlineData("../Custom.binds")]
        [InlineData("folder/Custom.binds")]
        [InlineData("folder\\Custom.binds")]
        [InlineData("C:Custom.binds")]
        [InlineData("Custom.txt")]
        [InlineData("")]
        public void Unsafe_paths_are_rejected(string path)
        {
            Assert.Throws<InvalidOperationException>(() => new EliteDangerousProfileBackend().Import("controls",
                new Dictionary<string, string> { [path] = "<Root/>" }));
        }

        /// <summary>Ambiguous captures, null inputs and oversized evidence cannot produce output.</summary>
        [Fact]
        public void Capture_boundary_rejects_invalid_collections()
        {
            var backend = new EliteDangerousProfileBackend();
            Assert.Throws<ArgumentException>(() => backend.Import(" ", Sources()));
            Assert.Throws<ArgumentNullException>(() => backend.Import("controls", null!));
            Assert.Throws<InvalidOperationException>(() => backend.Import("controls", new Dictionary<string, string>()));
            Assert.Throws<InvalidOperationException>(() => backend.Import("controls", new Dictionary<string, string> { ["StartPreset.4.start"] = "Custom" }));
            var files = Sources();
            files.Add("Other.binds", files["Synthetic.4.2.binds"]);
            Assert.Throws<InvalidOperationException>(() => backend.Import("controls", files));
            files = Sources();
            files["Synthetic.4.2.binds"] = new string('x', 4 * 1024 * 1024 + 1);
            Assert.Throws<InvalidOperationException>(() => backend.Import("controls", files));
            Assert.False(backend.Export(null!).Succeeded);
            Assert.Throws<ArgumentNullException>(() => backend.ResolveDevice(null!, []));
            Assert.Throws<ArgumentNullException>(() => backend.ResolveDevice(new(), null!));
        }

        /// <summary>Product names and ordinal indices never masquerade as unique instance evidence.</summary>
        [Fact]
        public void Device_resolution_requires_explicit_instance_evidence()
        {
            var backend = new EliteDangerousProfileBackend();
            var identity = backend.Import("controls", Sources()).Devices[0].Identity!;
            Assert.Equal("unresolved", backend.ResolveDevice(identity, [identity]).Status);
            identity.InstanceId = "instance-a";
            Assert.Equal("disconnected", backend.ResolveDevice(identity, []).Status);
            var other = new PhysicalDeviceIdentity { InstanceId = "INSTANCE-A" };
            Assert.Equal("resolved", backend.ResolveDevice(identity, [other]).Status);
            Assert.Same(other, Assert.Single(backend.ResolveDevice(identity, [other]).Matches));
            Assert.Equal("ambiguous", backend.ResolveDevice(identity, [other, other]).Status);
        }

        /// <summary>Cancellation propagates instead of becoming a conversion diagnostic.</summary>
        [Fact]
        public void Cancellation_propagates()
        {
            var backend = new EliteDangerousProfileBackend();
            var profile = backend.Import("controls", Sources());
            using var source = new CancellationTokenSource();
            source.Cancel();
            Assert.Throws<OperationCanceledException>(() => backend.Import("controls", Sources(), source.Token));
            Assert.Throws<OperationCanceledException>(() => backend.Export(profile, source.Token));
        }
        #endregion END Public

        #region BEGIN private helpers
        private static ProfileDocument Persist(ProfileDocument profile) => JsonSerializer.Deserialize<ProfileDocument>(JsonSerializer.Serialize(profile))!;

        private static Dictionary<string, string> Fixture() => new()
        {
            ["Synthetic.4.2.binds"] = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "EliteDangerous", "Synthetic.4.2.binds"))
        };

        private static Dictionary<string, string> Sources() => new()
        {
            ["StartPreset.4.start"] = "Custom\r\nCustom\r\nCustom\r\nCustom\r\n",
            ["Synthetic.4.2.binds"] = """
                <?xml version="1.0" encoding="UTF-8"?>
                <Root PresetName="Custom" MajorVersion="4" MinorVersion="2" Future="keep">
                  <Fire><Primary Device="ExampleStick" DeviceIndex="0" Key="Joy_1"><Hold Value="1" /></Primary><Secondary Device="Keyboard" Key="Key_Space" /><ToggleOn Value="1" /></Fire>
                  <Empty><Primary Device="{NoDevice}" Key="" /><Secondary Device="{NoDevice}" Key="" /></Empty>
                  <Pitch><Binding Device="ExampleStick" DeviceIndex="0" Key="Joy_YAxis" /><Inverted Value="1" /><Deadzone Value="0.123456789123" /></Pitch>
                  <FutureSetting Value="keep"><Nested/></FutureSetting><MouseGUI Value="0"/><MouseGUI Value="0"/>
                </Root>
                """
        };

        private static void Mutate(ProfileDocument profile, string change)
        {
            var stick = profile.Devices.Single(x => x.Axes.Count != 0);
            var button = stick.Buttons[0];
            var axis = stick.Axes[0];
            switch (change)
            {
                case "schema": profile.SchemaVersion++; break;
                case "backend": profile.BackendId = "dcs"; break;
                case "version": profile.BackendVersion++; break;
                case "context": profile.ContextId = "srv"; break;
                case "name": profile.Name = "renamed"; break;
                case "profileId": profile.ProfileId = "../escape.binds"; break;
                case "evidence": profile.NativeData.Clear(); break;
                case "evidenceNull": profile.NativeData["ed.v1"] = JsonSerializer.SerializeToElement<object?>(null); break;
                case "contextEvidence": profile.NativeData["ed.v1"] = JsonSerializer.SerializeToElement(new { ContextId = "srv", Sources = Sources() }); break;
                case "nativeDataNull": profile.NativeData = null!; break;
                case "device": stick.LogicalId = "unknown"; break;
                case "identity": stick.Identity!.SimSpecificIds["ed.device"] = "Other"; break;
                case "deviceContext": stick.ContextId = "srv"; break;
                case "systemRemoval": profile.Devices.RemoveAll(x => x.Identity!.SimSpecificIds["ed.device"] == "Keyboard"); break;
                case "duplicateDevice": profile.Devices[1] = stick; break;
                case "buttonsNull": stick.Buttons = null!; break;
                case "axesNull": stick.Axes = null!; break;
                case "devicesNull": profile.Devices = null!; break;
                case "deviceNull": profile.Devices[0] = null!; break;
                case "buttonNull": stick.Buttons[0] = null!; break;
                case "axisNull": stick.Axes[0] = null!; break;
                case "modifierNull": button.Modifiers.Add(null!); break;
                case "metadataNull": profile.Metadata = null!; break;
                case "extensionsNull": button.SimSpecific = null!; break;
                case "metadata": profile.Metadata["unsupported"] = "1"; break;
                case "metadataValue": profile.Metadata[profile.Metadata.Keys.Single()] = "yes"; break;
                case "mode": button.Mode = BindingMode.Toggle; break;
                case "slot": button.SimSpecific["ed.slot"] = "Binding"; break;
                case "extension": button.SimSpecific["future"] = "lost"; break;
                case "slotNull": button.SimSpecific["ed.slot"] = null!; break;
                case "key": button.SourceButton = ""; break;
                case "duplicateSlot": stick.Buttons.Add(button); break;
                case "command": button.CommandId = "unknown"; break;
                case "modifierDevice": button.Modifiers.Add(new() { Source = "unknown", Value = "Joy_2" }); break;
                case "modifierKey": button.Modifiers.Add(new() { Source = stick.LogicalId }); break;
                case "duplicateModifier": button.Modifiers.Add(new() { Source = stick.LogicalId, Value = "Joy_2" }); button.Modifiers.Add(button.Modifiers[0]); break;
                case "modifiersNull": button.Modifiers = null!; break;
                case "curve": axis.Curve = CurveType.Custom; break;
                case "sensitivity": axis.Sensitivity = 2; break;
                case "deadzoneNaN": axis.Deadzone = float.NaN; break;
                case "deadzoneLow": axis.Deadzone = -1; break;
                case "deadzoneHigh": axis.Deadzone = 101; break;
                case "axisModifier": axis.Modifiers.Add(new() { Source = stick.LogicalId, Value = "Joy_2" }); break;
                case "axisSlot": axis.SimSpecific["ed.slot"] = "Primary"; break;
                default: throw new InvalidOperationException(change);
            }
        }
        #endregion END private helpers
    }
}
