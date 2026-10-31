using System.Text.Json;
using System.Xml.Linq;
using Heimdall.Rendering;
using Heimdall.Squadrons;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Verifies read-only system diagrams, exact unknown inputs and lossless native presentation.</summary>
    public sealed class SquadronsSystemInputDrawingTests
    {
        #region BEGIN Public
        /// <summary>System artwork labels known controls, preserves unknown codes and separates contexts and devices.</summary>
        [Theory]
        [InlineData(0, 14, 8, "Pos_Mouse_ZAxis")]
        [InlineData(1, 26, 5, "A")]
        [InlineData(2, 26, 30, "Key_A")]
        public void System_artwork_is_contextual_and_does_not_modify_native_records(int type, int axis, int button, string key)
        {
            var flight = Row(type, axis, button, "Flight action", "Flight");
            var menus = Row(type, axis, button, "Menu action", "Menus & General");
            var unknown = Row(type, 26, 999, "Future action", "Flight");
            var rows = new[] { flight, menus, unknown, Row(3, 26, 30, "Joystick action", "Flight") };
            var frozen = JsonSerializer.Serialize(rows);
            var id = "system." + type;
            var diagram = SquadronsSystemInputDrawings.Create(id, rows)!;
            var control = diagram.Controls.Single(x => x.InputKey == key);
            Assert.Equal(new[] { flight }, SquadronsSystemInputDrawings.ForControl(control, rows, id, "Flight"));
            Assert.Equal(2, SquadronsSystemInputDrawings.ForControl(control, rows, id, "All").Count);
            var page = SquadronsSystemInputDrawings.Diagram("Systems", diagram, rows, id, "Flight");
            Assert.Contains(page.Labels[control.Control], label => label.Text == "Flight action");
            Assert.DoesNotContain(page.Labels.Values.SelectMany(x => x), label => label.Text == "Menu action" || label.Text == "Joystick action");
            Assert.Contains(diagram.Controls, x => x.Label.Contains("999", StringComparison.Ordinal));
            Assert.All(diagram.Controls, x => Assert.True(x.X >= 0 && x.Y >= 0 && x.X + x.Width <= diagram.Width && x.Y + x.Height <= diagram.Height));
            var composed = CompositeKneeboardRenderer.BuildDeviceSvg(diagram, page);
            Assert.NotNull(XDocument.Parse(composed).Root);
            Assert.NotEmpty(CanonicalSvgRenderer.RenderComplete(composed, diagram.Width, diagram.Height));
            Assert.Equal(frozen, JsonSerializer.Serialize(rows));
            Assert.Null(SquadronsSystemInputDrawings.Create("squadrons.joystick.1", rows));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static SquadronsWorkspaceRow Row(int type, int axis, int button, string name, string section) =>
            new(new SquadronsBindingRow("IncomStarshipInputConcepts.ConceptFire", 0, new Dictionary<string, int>
            {
                ["type"] = type, ["axis"] = axis, ["button"] = button, ["deviceid"] = -1,
                ["identifier"] = 123, ["altbutton"] = 0, ["modifier"] = 0, ["negate"] = 0
            }), name, section, "system." + type, "System");
        #endregion END private helpers
    }
}
