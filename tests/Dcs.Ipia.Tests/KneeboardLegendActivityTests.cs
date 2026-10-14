using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Checks continuous activity and scope isolation for kneeboard legend swatches.</summary>
    public sealed class KneeboardLegendActivityTests
    {
        #region BEGIN Public
        /// <summary>Hold, Toggle and combination colors remain lit for the full applicable state.</summary>
        [Fact]
        public void BaseAndShiftChordsFollowHoldAndToggleStates()
        {
            var physical = Guid.NewGuid();
            var session = new InputTestSession([], [
                new InputTestModifier(physical, AssignmentLayer.Module, "SHIFT", "JOY_BTN7", ModifierMode.Hold),
                new InputTestModifier(physical, AssignmentLayer.Module, "ARM", "JOY_BTN8", ModifierMode.Toggle),
                new InputTestModifier(physical, AssignmentLayer.UiLayer, "UI", "JOY_BTN9", ModifierMode.Toggle)
            ]);
            var baseEntry = new KneeboardModifierLegend("Base (no modifier)", "#ffffff");
            var shift = new KneeboardModifierLegend("SHIFT", "#dc2626", AssignmentLayer.Module, "SHIFT");
            var combo = new KneeboardModifierLegend("ARM + SHIFT", "#ea580c", AssignmentLayer.Module, "SHIFT+ARM");
            var uiBase = new KneeboardModifierLegend("UI Layer — Base", "#0891b2", AssignmentLayer.UiLayer);
            var ui = new KneeboardModifierLegend("UI", "#7c3aed", AssignmentLayer.UiLayer, "UI");
            void Sample(double hold, double arm, double uiKey) => session.Process([
                new DeviceInputFrame(physical, new Dictionary<string, double>
                {
                    ["JOY_BTN7"] = hold, ["JOY_BTN8"] = arm, ["JOY_BTN9"] = uiKey
                })
            ]);

            Sample(0, 0, 0);
            Assert.True(KneeboardLegendActivity.IsActive(baseEntry, session.ActiveModifiers));
            Assert.True(KneeboardLegendActivity.IsActive(uiBase, session.ActiveModifiers));
            Sample(1, 0, 0);
            Assert.True(KneeboardLegendActivity.IsActive(shift, session.ActiveModifiers));
            Assert.False(KneeboardLegendActivity.IsActive(baseEntry, session.ActiveModifiers));
            Sample(1, 1, 1);
            Assert.True(KneeboardLegendActivity.IsActive(combo, session.ActiveModifiers));
            Assert.True(KneeboardLegendActivity.IsActive(ui, session.ActiveModifiers));
            Sample(1, 0, 0);
            Assert.True(KneeboardLegendActivity.IsActive(combo, session.ActiveModifiers));
            Assert.True(KneeboardLegendActivity.IsActive(ui, session.ActiveModifiers));
            Sample(0, 0, 0);
            Assert.False(KneeboardLegendActivity.IsActive(combo, session.ActiveModifiers));
            Assert.True(KneeboardLegendActivity.IsActive(ui, session.ActiveModifiers));
            Sample(0, 1, 1);
            Assert.True(KneeboardLegendActivity.IsActive(baseEntry, session.ActiveModifiers));
            Assert.True(KneeboardLegendActivity.IsActive(uiBase, session.ActiveModifiers));
            Assert.False(KneeboardLegendActivity.IsActive(ui, session.ActiveModifiers));
            session.Reset();
            Assert.False(KneeboardLegendActivity.IsActive(shift, session.ActiveModifiers));
        }

        /// <summary>Same-named modifiers in different layers do not leak activity between scopes.</summary>
        [Fact]
        public void SameChordNameInDifferentScopesDoesNotLightWrongLegend()
        {
            var active = new Dictionary<AssignmentLayer, string>
            {
                [AssignmentLayer.Module] = "SHIFT",
                [AssignmentLayer.UiLayer] = ""
            };
            Assert.True(KneeboardLegendActivity.IsActive(
                new("Module", "#dc2626", AssignmentLayer.Module, "SHIFT"), active));
            Assert.False(KneeboardLegendActivity.IsActive(
                new("UI", "#7c3aed", AssignmentLayer.UiLayer, "SHIFT"), active));
        }
        #endregion END Public
    }
}
