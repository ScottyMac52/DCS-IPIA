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
