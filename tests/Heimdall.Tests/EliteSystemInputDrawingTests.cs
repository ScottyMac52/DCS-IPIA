using System.Text.Json;
using System.Xml.Linq;
using Heimdall.Application;
using Heimdall.EliteDangerous;
using Heimdall.Rendering;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Checks native keyboard/mouse geometry, exact keys, command labels and shared rendering.</summary>
    public sealed class EliteSystemInputDrawingTests
    {
        #region BEGIN Public
        /// <summary>System diagrams render complete reference cards without physical pins and preserve original bindings.</summary>
        [Theory]
        [InlineData("Keyboard")]
        [InlineData("Mouse")]
        public void System_artwork_uses_exact_inputs_and_visible_command_labels(string token)
        {
            var backend = new EliteDangerousProfileBackend(); var profile = Source(); var frozen = JsonSerializer.Serialize(profile);
            var device = profile.Devices.Single(x => x.Identity!.SimSpecificIds["ed.device"] == token);
            var diagram = EliteSystemInputDrawings.Create(profile, device.LogicalId)!;
            var catalog = backend.GetCatalog(profile);
            var rows = EliteWorkspaceProjection.Rows(profile, catalog).Where(x => x.DeviceId == device.LogicalId).ToArray();
            Assert.NotEmpty(diagram.Controls); Assert.Equal(diagram.Controls.Count, diagram.Controls.Select(x => x.InputKey).Distinct(StringComparer.Ordinal).Count());
            Assert.All(diagram.Controls, c => { Assert.True(c.X >= 0 && c.Y >= 0 && c.X + c.Width <= diagram.Width && c.Y + c.Height <= diagram.Height); Assert.Equal(c.InputKey, EliteInputKeys.FromDiagram(diagram, c)); });
            var page = EliteWorkspaceProjection.Diagram("Systems", diagram, rows, "All");
            var sheet = EliteReferenceSheetBuilder.Build(profile, catalog, device.LogicalId, token, "All", true);
            foreach (var row in rows)
            {
                var control = diagram.Controls.Single(x => x.InputKey == row.Input);
                Assert.Contains(page.Labels[control.Control], label => label.Text.Contains(row.CommandName, StringComparison.Ordinal));
                Assert.Contains(sheet.Rows, r => r.Command.Contains(row.CommandName, StringComparison.Ordinal) && r.Input.Contains(row.Input, StringComparison.Ordinal));
            }
            var composed = XDocument.Parse(CompositeKneeboardRenderer.BuildDeviceSvg(diagram, page));
            foreach (var control in diagram.Controls)
            {
                var label = composed.Descendants().Single(x => (string?)x.Attribute("id") == "lbl-" + control.Control);
                Assert.DoesNotContain("#", label.Value);
                foreach (var span in label.Elements())
                {
                    var y = double.Parse(span.Attribute("y")!.Value, System.Globalization.CultureInfo.InvariantCulture);
                    Assert.True(y > control.Y + control.LabelTopInset && y < control.Y + control.Height);
                }
            }
            var sharedInput = token == "Keyboard" ? "Key_A" : "Mouse_1";
            var sharedControl = diagram.Controls.Single(x => x.InputKey == sharedInput);
            Assert.True(page.Labels[sharedControl.Control].Count >= 2);
            var image = CompositeKneeboardRenderer.Render(diagram, page);
            Assert.True(image.Length > 1000);
            var cards = ReferenceSheetRenderer.Render(sheet, new("A4", true, true), image);
            Assert.True(cards.PageCount >= 2); Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(cards.Pdf, 0, 5));
            Assert.Equal(frozen, JsonSerializer.Serialize(profile)); Assert.True(backend.Export(profile).Succeeded);
        }
        /// <summary>Malformed label padding fails without accepting an invalid drawing area.</summary>
        [Theory]
        [InlineData(-1)]
        [InlineData(70)]
        [InlineData(double.NaN)]
        public void Invalid_caption_insets_are_rejected(double inset)
        {
            var profile = Source(); var keyboard = profile.Devices.Single(x => x.Identity!.SimSpecificIds["ed.device"] == "Keyboard");
            var drawing = EliteSystemInputDrawings.Create(profile, keyboard.LogicalId)!;
            var control = drawing.Controls.Single(x => x.InputKey == "Key_A") with { LabelTopInset = inset, Height = 70 };
            drawing = drawing with { Controls = [control] };
            var page = EliteWorkspaceProjection.Diagram("Systems", drawing,
                EliteWorkspaceProjection.Rows(profile, new EliteDangerousProfileBackend().GetCatalog(profile)).Where(x => x.DeviceId == keyboard.LogicalId).ToArray(), "All");
            Assert.Throws<InvalidDataException>(() => CompositeKneeboardRenderer.BuildDeviceSvg(drawing, page));
        }
        /// <summary>Keyboard letters, modifiers and keypad keys remain distinct; unknown imported names are escaped and retained.</summary>
        [Fact]
        public void Keyboard_layout_preserves_native_names_and_additional_imported_inputs()
        {
            var profile = Source(); var keyboard = profile.Devices.Single(x => x.Identity!.SimSpecificIds["ed.device"] == "Keyboard");
            var diagram = EliteSystemInputDrawings.Create(profile, keyboard.LogicalId)!;
            foreach (var key in new[] { "Key_A", "Key_LeftShift", "Key_RightShift", "Key_Numpad_0", "Key_0", "Key_F12", "Key_backslash", "Key_Future<&" })
                Assert.Contains(diagram.Controls, x => x.InputKey == key);
            var xml = XDocument.Parse(diagram.Svg); Assert.Contains(xml.Descendants(), x => x.Name.LocalName == "text" && x.Value == "Key_Future<&");
            Assert.Equal(diagram.Svg, EliteSystemInputDrawings.Create(profile, keyboard.LogicalId)!.Svg);
            var stick = profile.Devices.Single(x => x.Identity!.SimSpecificIds["ed.device"] == "ExampleStick");
            Assert.Null(EliteSystemInputDrawings.Create(profile, stick.LogicalId));
            Assert.Throws<InvalidOperationException>(() => EliteSystemInputDrawings.Create(profile, "missing"));
            Assert.Null(EliteInputKeys.FromDiagram(diagram with { Path = "shared/controller.svg" }, diagram.Controls[0]));
        }
        /// <summary>Mouse buttons, wheel directions and captured axes have independent clickable controls.</summary>
        [Fact]
        public void Mouse_buttons_wheel_and_imported_axes_are_not_inferred_from_joystick_names()
        {
            var profile = Source(); var mouse = profile.Devices.Single(x => x.Identity!.SimSpecificIds["ed.device"] == "Mouse");
            var diagram = EliteSystemInputDrawings.Create(profile, mouse.LogicalId)!;
            foreach (var key in new[] { "Mouse_1", "Mouse_2", "Mouse_3", "Pos_Mouse_ZAxis", "Neg_Mouse_ZAxis" })
                Assert.Contains(diagram.Controls, x => x.InputKey == key && !x.IsAxis);
            Assert.Contains(diagram.Controls, x => x.InputKey == "Mouse_XAxis" && x.IsAxis);
            var catalog = new EliteDangerousProfileBackend().GetCatalog(profile);
            var rows = EliteWorkspaceProjection.Rows(profile, catalog).Where(x => x.DeviceId == mouse.LogicalId).ToArray();
            var empty = EliteWorkspaceProjection.Diagram("Systems", diagram, rows, "On Foot");
            Assert.All(empty.Labels.Values, Assert.Empty);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static Heimdall.Domain.MultiSim.ProfileDocument Source() => new EliteDangerousProfileBackend().Import("controls", new Dictionary<string, string>
        {
            ["Systems.4.2.binds"] = """
            <Root PresetName="Systems" MajorVersion="4" MinorVersion="2">
              <PrimaryFire><Primary Device="Keyboard" Key="Key_A"><Modifier Device="Keyboard" Key="Key_LeftShift"/></Primary><Secondary Device="Mouse" Key="Mouse_1"/></PrimaryFire>
              <SecondaryFire><Primary Device="Mouse" Key="Pos_Mouse_ZAxis"/><Secondary Device="Keyboard" Key="Key_Future&lt;&amp;"/></SecondaryFire>
              <PitchAxisRaw><Binding Device="Mouse" Key="Mouse_XAxis"/><Inverted Value="0"/><Deadzone Value="0"/></PitchAxisRaw>
              <DeployHardpointToggle><Primary Device="Mouse" Key="Mouse_1"/><Secondary Device="Keyboard" Key="Key_A"/></DeployHardpointToggle>
              <LandingGearToggle><Primary Device="ExampleStick" Key="Joy_1"/><Secondary Device="{NoDevice}" Key=""/></LandingGearToggle>
            </Root>
            """
        });
        #endregion END private helpers
    }
}
