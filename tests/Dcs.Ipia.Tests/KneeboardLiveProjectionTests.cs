using Dcs.Ipia.Application;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Ensures that only the currently applicable modifier layers receive saturated text/highlights.</summary>
    public sealed class KneeboardLiveProjectionTests
    {
        #region BEGIN Public
        /// <summary>Base input is yellow; shifted input uses its corresponding red legend swatch.</summary>
        [Fact]
        public void BaseAndShiftUseExclusiveLiveColors()
        {
            var page = Example();
            var baseline = new Dictionary<AssignmentLayer, string>();
            Assert.Equal(KneeboardLiveProjection.BaseHighlightColor,
                KneeboardLiveProjection.ActiveColor(page, "button", baseline));
            Assert.Null(KneeboardLiveProjection.ActiveColor(page, "shift-only", baseline));
            var baseImage = KneeboardLiveProjection.Focus(page, baseline);
            Assert.Equal("#ffffff", baseImage.Labels["button"][0].Color);
            Assert.Equal(KneeboardLiveProjection.InactiveLabelColor, baseImage.Labels["button"][1].Color);

            var held = new Dictionary<AssignmentLayer, string> { [AssignmentLayer.Module] = "S3" };
            Assert.Equal("#dc2626", KneeboardLiveProjection.ActiveColor(page, "button", held));
            Assert.Equal("#dc2626", KneeboardLiveProjection.ActiveColor(page, "shift-only", held));
            var shiftedImage = KneeboardLiveProjection.Focus(page, held);
            Assert.Equal(KneeboardLiveProjection.InactiveLabelColor, shiftedImage.Labels["button"][0].Color);
            Assert.Equal("#dc2626", shiftedImage.Labels["button"][1].Color);
            Assert.Equal("#ffffff", page.Labels["button"][0].Color); // Immutable export source.
        }

        /// <summary>Combined chords and UI scope cannot illuminate a nonmatching physical region.</summary>
        [Fact]
        public void CombinedChordsAndIndependentUiScopes()
        {
            var page = Example();
            var active = new Dictionary<AssignmentLayer, string> {
                [AssignmentLayer.Module] = "ARM+S3", [AssignmentLayer.UiLayer] = "UI"
            };
            Assert.Null(KneeboardLiveProjection.ActiveColor(page, "button", active));
            Assert.Equal("#7c3aed", KneeboardLiveProjection.ActiveColor(page, "ui", active));
            Assert.Equal(KneeboardLiveProjection.BaseHighlightColor,
                KneeboardLiveProjection.ActiveColor(page, "unmapped", active));
            active[AssignmentLayer.Module] = "S3";
            Assert.Equal("#dc2626", KneeboardLiveProjection.ActiveColor(page, "button", active));
            Assert.Equal("#7c3aed", KneeboardLiveProjection.ActiveColor(page, "ui", active));
        }

        /// <summary>UI Layer SHIFT replaces Module Base for paired positions of one physical button.</summary>
        [Fact]
        public void UiShiftWinsOverModuleBaseAcrossPhysicalCalloutPair()
        {
            var page = new KneeboardComposite("MFD", "UI", new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>>
            {
                ["base"] = [new("Normal", "#ffffff", AssignmentLayer.Module, "")],
                ["shifted"] = [new("UI SHIFT", "#7c3aed", AssignmentLayer.UiLayer, "UI_SHIFT")]
            }, new Dictionary<string, string>(), [
                new("Base", "#ffffff", AssignmentLayer.Module),
                new("UI SHIFT", "#7c3aed", AssignmentLayer.UiLayer, "UI_SHIFT")
            ]);
            HardwareGraphicControl[] controls = [
                new("base", "Normal", "JOY_BTN1", 0, 0, 20, 20),
                new("shifted", "Shifted", "JOY_BTN1", 0, 30, 20, 20)
            ];
            var active = new Dictionary<AssignmentLayer, string>();
            Assert.Equal(KneeboardLiveProjection.BaseHighlightColor,
                KneeboardLiveProjection.ActiveColor(page, "base", controls, active));
            Assert.Null(KneeboardLiveProjection.ActiveColor(page, "shifted", controls, active));

            active[AssignmentLayer.UiLayer] = "UI_SHIFT";
            Assert.Null(KneeboardLiveProjection.ActiveColor(page, "base", controls, active));
            Assert.Equal("#7c3aed", KneeboardLiveProjection.ActiveColor(page, "shifted", controls, active));
            var focused = KneeboardLiveProjection.Focus(page, controls, active);
            Assert.Equal(KneeboardLiveProjection.InactiveLabelColor, focused.Labels["base"][0].Color);
            Assert.Equal("#7c3aed", focused.Labels["shifted"][0].Color);

            active[AssignmentLayer.UiLayer] = "";
            Assert.Equal(KneeboardLiveProjection.BaseHighlightColor,
                KneeboardLiveProjection.ActiveColor(page, "base", controls, active));
            Assert.Null(KneeboardLiveProjection.ActiveColor(page, "shifted", controls, active));
            Assert.Equal("#ffffff", page.Labels["base"][0].Color);
        }

        /// <summary>Independent Module and UI Layer chords select the corresponding real assignment.</summary>
        [Fact]
        public void ModuleAndUiShiftUseIndependentScopesAndPriority()
        {
            var page = new KneeboardComposite("MFD", "UI", new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>>
            {
                ["base"] = [new("Normal", "#ffffff", AssignmentLayer.Module, "")],
                ["module"] = [new("Module shift", "#dc2626", AssignmentLayer.Module, "S3")],
                ["ui"] = [new("UI shift", "#7c3aed", AssignmentLayer.UiLayer, "UI_SHIFT")]
            }, new Dictionary<string, string>(), []);
            HardwareGraphicControl[] controls = [
                new("base", "Normal", "JOY_BTN1", 0, 0, 20, 20),
                new("module", "Module", "JOY_BTN1", 0, 25, 20, 20),
                new("ui", "UI", "JOY_BTN1", 0, 50, 20, 20)
            ];
            var state = new Dictionary<AssignmentLayer, string> { [AssignmentLayer.Module] = "S3" };
            Assert.Equal("#dc2626", KneeboardLiveProjection.ActiveColor(page, "module", controls, state));
            Assert.Null(KneeboardLiveProjection.ActiveColor(page, "base", controls, state));
            state[AssignmentLayer.UiLayer] = "UI_SHIFT";
            Assert.Equal("#7c3aed", KneeboardLiveProjection.ActiveColor(page, "ui", controls, state));
            Assert.Null(KneeboardLiveProjection.ActiveColor(page, "module", controls, state));
            state[AssignmentLayer.Module] = "";
            Assert.Equal("#7c3aed", KneeboardLiveProjection.ActiveColor(page, "ui", controls, state));
            state[AssignmentLayer.UiLayer] = "OTHER";
            Assert.Equal(KneeboardLiveProjection.BaseHighlightColor,
                KneeboardLiveProjection.ActiveColor(page, "base", controls, state));
        }

        /// <summary>UI Layer Base also receives the visible yellow Base outline.</summary>
        [Fact]
        public void UiBaseHasYellowOutlineAndExactScopeMatching()
        {
            var page = new KneeboardComposite("MFD", "UI", new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>>
            {
                ["ui"] = [new("UI Base", "#0891b2", AssignmentLayer.UiLayer, "")]
            }, new Dictionary<string, string>(), []);
            HardwareGraphicControl[] controls = [new("ui", "UI", "JOY_BTN2", 0, 0, 20, 20)];
            var state = new Dictionary<AssignmentLayer, string> { [AssignmentLayer.Module] = "S3" };
            Assert.Equal(KneeboardLiveProjection.BaseHighlightColor,
                KneeboardLiveProjection.ActiveColor(page, "ui", controls, state));
            state[AssignmentLayer.UiLayer] = "UI_SHIFT";
            Assert.Null(KneeboardLiveProjection.ActiveColor(page, "ui", controls, state));
        }
        #endregion END Public

        #region BEGIN private helpers
        private static KneeboardComposite Example() => new(
            "Test", "MFD", new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>> {
                ["button"] = [
                    new("Base", "#ffffff", AssignmentLayer.Module, ""),
                    new("Shift", "#dc2626", AssignmentLayer.Module, "S3")
                ],
                ["shift-only"] = [new("Only shift", "#dc2626", AssignmentLayer.Module, "S3")],
                ["ui"] = [new("UI", "#7c3aed", AssignmentLayer.UiLayer, "UI")]
            },
            new Dictionary<string, string>(), []);
        #endregion END private helpers
    }
}
