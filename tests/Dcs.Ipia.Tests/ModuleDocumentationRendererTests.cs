using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Rendering;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Checks guide semantics, escaping, sparse filters, captured provenance and explicit integrations.</summary>
    public sealed class ModuleDocumentationRendererTests
    {
        #region BEGIN Public
        /// <summary>Guides preserve history/defaults and safely display authored text without fabricated integrations.</summary>
        [Fact]
        public void Guides_describe_actual_output_and_escape_authored_text()
        {
            var id = Guid.NewGuid();
            var module = Guid.NewGuid();
            var slot = new DraftAssignmentSlot(module, id, AssignmentLayer.Module, BindingSection.Axis, "JOY_X", "A+B");
            var workspace = new EditorWorkspaceSnapshot(new(new(module, "DOCS", "Guide aircraft"), [], [], [], "local source"),
                [], [], [], [], [], [], new Dictionary<string, string> { [slot.Key] = "Pitch | <script>\n*[link](url)" });
            var profile = new ModuleDocumentationProfile("Config/Input/DOCS/joystick/Stick.diff.lua", id,
                [new(id, BindingSection.Axis, "axis", "Pitch", BindingDisposition.Changed, "JOY_X", ["B", "A"],
                    new(Deadzone: 3, SaturationX: 90, SaturationY: 95, Curvature: [12, 15], Invert: true, Slider: false,
                        HardwareDetent: false, HardwareDetentAB: 40, HardwareDetentMax: 80)),
                 new(id, BindingSection.Key, "key", "Old command", BindingDisposition.Removed, "JOY_BTN1"),
                 new(id, BindingSection.Key, "key2", "New command", BindingDisposition.Added, "JOY_BTN2")],
                [new("A", "Keyboard", "LShift", ModifierMode.Hold)]);
            var sources = new Dictionary<string, string> { ["LICENSE.txt"] = "Captured license\r\nCopyright | holder\n```", ["metadata.json"] = "{}" };
            workspace = workspace with { Sources = sources };
            var defaults = new ModuleDocumentationProfile("default/Keyboard.diff.lua", null,
                [new(id, BindingSection.Key, "pause", "Pause", BindingDisposition.Added, "Pause")], []);
            var guides = ModuleDocumentationRenderer.Render(workspace, "1.2", "digest", [profile], [defaults]);
            Assert.Equal(GuideNames, guides.Keys);
            var controls = guides["CONTROL-MAPPINGS.md"];
            Assert.Contains("Pitch \\| &lt;script&gt;<br>\\*\\[link\\](url)", controls, StringComparison.Ordinal);
            Assert.Contains("A + B", controls, StringComparison.Ordinal);
            Assert.Contains("Removed", controls, StringComparison.Ordinal);
            Assert.Contains("Changed", controls, StringComparison.Ordinal);
            Assert.Contains("HardwareDetentAB", controls, StringComparison.Ordinal);
            Assert.Contains("Pause", controls, StringComparison.Ordinal);
            Assert.Contains("No separately captured", controls, StringComparison.Ordinal);
            Assert.Contains("Captured license", guides["THIRD-PARTY-ASSETS.md"], StringComparison.Ordinal);
            Assert.Contains("HEIMDALL rendering font license", guides["THIRD-PARTY-ASSETS.md"], StringComparison.Ordinal);
            Assert.DoesNotContain("VAICOM", string.Concat(guides.Values), StringComparison.Ordinal);
            Assert.DoesNotContain("\r", string.Concat(guides.Values), StringComparison.Ordinal);
            Assert.Equal(guides.OrderBy(x => x.Key), ModuleDocumentationRenderer.Render(workspace, "1.2", "digest", [profile], [defaults]).OrderBy(x => x.Key));
        }

        /// <summary>Only explicit, valid captured metadata adds an integration guide.</summary>
        [Fact]
        public void Integrations_require_explicit_module_metadata_and_instructions()
        {
            var workspace = new EditorWorkspaceSnapshot(new(new(Guid.NewGuid(), "DOCS", "Guide aircraft"), [], [], [], "local"),
                [], [], [], [], [], [], new Dictionary<string, string>());
            var empty = ModuleDocumentationRenderer.Render(workspace, "1", "digest", [], []);
            Assert.DoesNotContain("INTEGRATIONS.md", empty.Keys);
            Assert.Contains("No license or attribution statement", empty["THIRD-PARTY-ASSETS.md"], StringComparison.Ordinal);
            EditorWorkspaceSnapshot With(string value) => workspace with { Sources = new Dictionary<string, string> { ["config/integrations.json"] = value } };
            var declared = ModuleDocumentationRenderer.Render(With("""{"schemaVersion":1,"integrations":[{"name":"OpenKneeboard","instructions":"Use the declared folder.\nKeep this custom mapping."}]}"""), "1", "digest", [], []);
            Assert.Contains("OpenKneeboard", declared["INTEGRATIONS.md"], StringComparison.Ordinal);
            Assert.Contains("INTEGRATIONS.md", declared["README.md"], StringComparison.Ordinal);
            Assert.Throws<InvalidDataException>(() => ModuleDocumentationRenderer.Render(With("""{"schemaVersion":2,"integrations":[]}"""), "1", "digest", [], []));
            Assert.Throws<InvalidDataException>(() => ModuleDocumentationRenderer.Render(With("""{"schemaVersion":1,"integrations":[{"name":"VAICOM","instructions":" "}]}"""), "1", "digest", [], []));
            Assert.Throws<ArgumentNullException>(() => ModuleDocumentationRenderer.Render(null!, "1", "digest", [], []));
            Assert.Throws<ArgumentNullException>(() => ModuleDocumentationRenderer.Render(workspace, "1", "digest", null!, []));
            Assert.Throws<ArgumentNullException>(() => ModuleDocumentationRenderer.Render(workspace, "1", "digest", [], null!));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static readonly string[] GuideNames = ["CONTROL-MAPPINGS.md", "INSTALLATION.md", "README.md", "THIRD-PARTY-ASSETS.md"];
        #endregion END private helpers
    }
}
