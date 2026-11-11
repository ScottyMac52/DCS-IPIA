using Heimdall.Application;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Ensures that only the currently applicable modifier layers receive saturated text/highlights.</summary>
    public sealed class KneeboardLiveProjectionTests
    {
        #region BEGIN Public
        /// <summary>Base input is blue; shifted input uses its corresponding red legend swatch.</summary>
        [Fact]
        public void BaseAndShiftUseExclusiveLiveColors()
        {
            var page = Example();
            var baseline = new Dictionary<AssignmentLayer, string>();
            Assert.Equal(DcsKneeboardLiveProjection.BaseHighlightColor,
                DcsKneeboardLiveProjection.ActiveColor(page, "button", baseline));
            Assert.Null(DcsKneeboardLiveProjection.ActiveColor(page, "shift-only", baseline));
            var baseImage = DcsKneeboardLiveProjection.Focus(page, baseline);
            Assert.Equal("#ffffff", baseImage.Labels["button"][0].Color);
            Assert.Equal(DcsKneeboardLiveProjection.InactiveLabelColor, baseImage.Labels["button"][1].Color);

            var held = new Dictionary<AssignmentLayer, string> { [AssignmentLayer.Module] = "S3" };
            Assert.Equal("#dc2626", DcsKneeboardLiveProjection.ActiveColor(page, "button", held));
            Assert.Equal("#dc2626", DcsKneeboardLiveProjection.ActiveColor(page, "shift-only", held));
            var shiftedImage = DcsKneeboardLiveProjection.Focus(page, held);
            Assert.Equal(DcsKneeboardLiveProjection.InactiveLabelColor, shiftedImage.Labels["button"][0].Color);
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
            Assert.Null(DcsKneeboardLiveProjection.ActiveColor(page, "button", active));
            Assert.Equal("#7c3aed", DcsKneeboardLiveProjection.ActiveColor(page, "ui", active));
            Assert.Equal(DcsKneeboardLiveProjection.BaseHighlightColor,
                DcsKneeboardLiveProjection.ActiveColor(page, "unmapped", active));
            active[AssignmentLayer.Module] = "S3";
            Assert.Equal("#dc2626", DcsKneeboardLiveProjection.ActiveColor(page, "button", active));
            Assert.Equal("#7c3aed", DcsKneeboardLiveProjection.ActiveColor(page, "ui", active));
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
            Assert.Equal(DcsKneeboardLiveProjection.BaseHighlightColor,
                DcsKneeboardLiveProjection.ActiveColor(page, "base", controls, active));
            Assert.Null(DcsKneeboardLiveProjection.ActiveColor(page, "shifted", controls, active));

            active[AssignmentLayer.UiLayer] = "UI_SHIFT";
            Assert.Null(DcsKneeboardLiveProjection.ActiveColor(page, "base", controls, active));
            Assert.Equal("#7c3aed", DcsKneeboardLiveProjection.ActiveColor(page, "shifted", controls, active));
            var focused = DcsKneeboardLiveProjection.Focus(page, controls, active);
            Assert.Equal(DcsKneeboardLiveProjection.InactiveLabelColor, focused.Labels["base"][0].Color);
            Assert.Equal("#7c3aed", focused.Labels["shifted"][0].Color);

            active[AssignmentLayer.UiLayer] = "";
            Assert.Equal(DcsKneeboardLiveProjection.BaseHighlightColor,
                DcsKneeboardLiveProjection.ActiveColor(page, "base", controls, active));
            Assert.Null(DcsKneeboardLiveProjection.ActiveColor(page, "shifted", controls, active));
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
            Assert.Equal("#dc2626", DcsKneeboardLiveProjection.ActiveColor(page, "module", controls, state));
            Assert.Null(DcsKneeboardLiveProjection.ActiveColor(page, "base", controls, state));
            state[AssignmentLayer.UiLayer] = "UI_SHIFT";
            Assert.Equal("#7c3aed", DcsKneeboardLiveProjection.ActiveColor(page, "ui", controls, state));
            Assert.Null(DcsKneeboardLiveProjection.ActiveColor(page, "module", controls, state));
            state[AssignmentLayer.Module] = "";
            Assert.Equal("#7c3aed", DcsKneeboardLiveProjection.ActiveColor(page, "ui", controls, state));
            state[AssignmentLayer.UiLayer] = "OTHER";
            Assert.Equal(DcsKneeboardLiveProjection.BaseHighlightColor,
                DcsKneeboardLiveProjection.ActiveColor(page, "base", controls, state));
        }

        /// <summary>UI Layer Base keeps its cyan identity and exact scope matching.</summary>
        [Fact]
        public void UiBaseKeepsCyanAndExactScopeMatching()
        {
            var page = new KneeboardComposite("MFD", "UI", new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>>
            {
                ["ui"] = [new("UI Base", "#0891b2", AssignmentLayer.UiLayer, "")]
            }, new Dictionary<string, string>(), []);
            HardwareGraphicControl[] controls = [new("ui", "UI", "JOY_BTN2", 0, 0, 20, 20)];
            var state = new Dictionary<AssignmentLayer, string> { [AssignmentLayer.Module] = "S3" };
            Assert.Equal("#0891b2",
                DcsKneeboardLiveProjection.ActiveColor(page, "ui", controls, state));
            state[AssignmentLayer.UiLayer] = "UI_SHIFT";
            Assert.Null(DcsKneeboardLiveProjection.ActiveColor(page, "ui", controls, state));
        }

        /// <summary>Modifier activator buttons keep their swatch color and light their callout when held or toggled.</summary>
        [Fact]
        public void ModifierActivatorsLightCalloutAndRetainSwatchWhenActive()
        {
            var page = new KneeboardComposite("Stick", "TEST", new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>>
            {
                ["s3"] = [new("S3 (Hold)", "#dc2626", AssignmentLayer.Module, "")],
                ["unused"] = [new("UNUSED (Hold)", KneeboardStateColors.UnusedModifier, AssignmentLayer.Module, "")],
                ["shared"] = [
                    new("Base action", "#ffffff", AssignmentLayer.Module, ""),
                    new("ARM (Toggle)", "#ea580c", AssignmentLayer.Module, "")
                ],
                ["ui-mod"] = [new("UI: UI_MOD (Toggle)", "#7c3aed", AssignmentLayer.UiLayer, "")]
            }, new Dictionary<string, string>(), []);
            HardwareGraphicControl[] controls = [
                new("s3", "Pinkie", "JOY_BTN3", 0, 0, 20, 20),
                new("unused", "Unused", "JOY_BTN4", 0, 25, 20, 20),
                new("shared", "Shared", "JOY_BTN5", 0, 50, 20, 20),
                new("ui-mod", "UI Mod", "JOY_BTN6", 0, 75, 20, 20)
            ];

            var baseline = new Dictionary<AssignmentLayer, string>();
            Assert.Equal("#dc2626", DcsKneeboardLiveProjection.ActiveColor(page, "s3", controls, baseline));
            Assert.Equal(DcsKneeboardLiveProjection.BaseHighlightColor,
                DcsKneeboardLiveProjection.ActiveColor(page, "shared", controls, baseline));

            var heldS3 = new Dictionary<AssignmentLayer, string> { [AssignmentLayer.Module] = "S3" };
            Assert.Equal("#dc2626", DcsKneeboardLiveProjection.ActiveColor(page, "s3", heldS3));
            Assert.Equal("#dc2626", DcsKneeboardLiveProjection.ActiveColor(page, "s3", controls, heldS3));
            Assert.Equal("#dc2626", DcsKneeboardLiveProjection.HighlightColor(page, "s3", controls, heldS3));
            var focusedS3 = DcsKneeboardLiveProjection.Focus(page, controls, heldS3);
            Assert.Equal("#dc2626", focusedS3.Labels["s3"][0].Color);

            var heldUnused = new Dictionary<AssignmentLayer, string> { [AssignmentLayer.Module] = "UNUSED" };
            Assert.Equal(KneeboardStateColors.UnusedModifier,
                DcsKneeboardLiveProjection.ActiveColor(page, "unused", controls, heldUnused));

            var toggledArm = new Dictionary<AssignmentLayer, string> { [AssignmentLayer.Module] = "ARM" };
            Assert.Equal("#ea580c", DcsKneeboardLiveProjection.ActiveColor(page, "shared", controls, toggledArm));
            var focusedArm = DcsKneeboardLiveProjection.Focus(page, controls, toggledArm);
            Assert.Equal(DcsKneeboardLiveProjection.InactiveLabelColor, focusedArm.Labels["shared"][0].Color);
            Assert.Equal("#ea580c", focusedArm.Labels["shared"][1].Color);

            var toggledUi = new Dictionary<AssignmentLayer, string> { [AssignmentLayer.UiLayer] = "UI_MOD" };
            Assert.Equal("#7c3aed", DcsKneeboardLiveProjection.ActiveColor(page, "ui-mod", controls, toggledUi));
            var focusedUi = DcsKneeboardLiveProjection.Focus(page, controls, toggledUi);
            Assert.Equal("#7c3aed", focusedUi.Labels["ui-mod"][0].Color);
        }

        /// <summary>Physical input highlights fall back to the active base or shifted callout when unassigned in the current chord.</summary>
        [Fact]
        public void HighlightColorFallsBackToMatchingPhysicalCalloutWhenChordIsUnassigned()
        {
            var page = new KneeboardComposite("Test", "MFD", new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>>
            {
                ["shift-only"] = [new("Only shift", "#dc2626", AssignmentLayer.Module, "S3")],
                ["base-only"] = [new("Only base", "#ffffff", AssignmentLayer.Module, "")]
            }, new Dictionary<string, string>(), []);
            HardwareGraphicControl[] controls = [
                new("shift-only", "Shift only", "JOY_BTN1", 0, 0, 20, 20),
                new("base-only", "Base only", "JOY_BTN2", 0, 25, 20, 20),
                new("pair", "Pair base", "JOY_BTN3", 0, 50, 20, 20),
                new("pair-shifted", "Pair shifted", "JOY_BTN3", 0, 75, 20, 20)
            ];

            var baseline = new Dictionary<AssignmentLayer, string>();
            Assert.Equal(DcsKneeboardLiveProjection.BaseHighlightColor,
                DcsKneeboardLiveProjection.HighlightColor(page, "shift-only", controls, baseline));
            Assert.Equal(DcsKneeboardLiveProjection.BaseHighlightColor,
                DcsKneeboardLiveProjection.HighlightColor(page, "pair", controls, baseline));
            Assert.Null(DcsKneeboardLiveProjection.HighlightColor(page, "pair-shifted", controls, baseline));
            Assert.Equal(DcsKneeboardLiveProjection.BaseHighlightColor,
                DcsKneeboardLiveProjection.ActiveColor(page, "pair", controls, baseline));
            Assert.Null(DcsKneeboardLiveProjection.ActiveColor(page, "pair-shifted", controls, baseline));

            var held = new Dictionary<AssignmentLayer, string> { [AssignmentLayer.Module] = "S3" };
            Assert.Equal("#dc2626", DcsKneeboardLiveProjection.HighlightColor(page, "shift-only", controls, held));
            Assert.Equal(DcsKneeboardLiveProjection.BaseHighlightColor,
                DcsKneeboardLiveProjection.HighlightColor(page, "base-only", controls, held));
            Assert.Null(DcsKneeboardLiveProjection.HighlightColor(page, "pair", controls, held));
            Assert.Equal(DcsKneeboardLiveProjection.BaseHighlightColor,
                DcsKneeboardLiveProjection.HighlightColor(page, "pair-shifted", controls, held));
            Assert.Null(DcsKneeboardLiveProjection.ActiveColor(page, "pair", controls, held));
            Assert.Equal(DcsKneeboardLiveProjection.BaseHighlightColor,
                DcsKneeboardLiveProjection.ActiveColor(page, "pair-shifted", controls, held));
            Assert.Null(DcsKneeboardLiveProjection.HighlightColor(page, "unknown", controls, held));
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
