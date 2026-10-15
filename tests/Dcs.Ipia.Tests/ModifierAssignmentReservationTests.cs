using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Regression tests for physical-input reservation across module and UI Layer scopes.</summary>
    public sealed class ModifierAssignmentReservationTests
    {
        #region BEGIN Public
        /// <summary>One physical input can be held by multiple scoped modifiers and all descriptions remain visible.</summary>
        [Fact]
        public void BothScopesReserveMatchingPhysicalButtonAcrossAliases()
        {
            var id = Guid.NewGuid();
            var state = new WorkspaceManagement([
                new(id, AssignmentLayer.Module, "F16 MFD {guid}.diff.lua", null, null),
                new(id, AssignmentLayer.UiLayer, "F16 MFD {guid}.diff.lua", null, null)
            ], [
                new(AssignmentLayer.Module, id, "S3", "JOY_BTN_POV1_U", "S3", ModifierMode.Hold, ""),
                new(AssignmentLayer.UiLayer, id, "UI SHIFT", "JOY_POV1_U", "UI", ModifierMode.Toggle, "")
            ]);
            var matches = ModifierAssignmentReservation.Find(state, id, "JOY_POV1_U");
            Assert.Equal(2, matches.Count);
            Assert.Contains("S3 (Hold)", ModifierAssignmentReservation.BlockReason("JOY_POV1_U", matches));
            Assert.Contains("UI SHIFT (Toggle)", ModifierAssignmentReservation.BlockReason("JOY_POV1_U", matches));
            Assert.Empty(ModifierAssignmentReservation.Find(state, id, "JOY_BTN3"));
        }

        /// <summary>Equal button numbers on unrelated controllers cannot reserve each other's controls.</summary>
        [Fact]
        public void DeviceIdentityIsolatedAndSharedPhysicalGuidRecognized()
        {
            var one = Guid.NewGuid();
            var alias = Guid.NewGuid();
            var other = Guid.NewGuid();
            var guid = Guid.NewGuid();
            var state = new WorkspaceManagement([
                new(one, AssignmentLayer.Module, "One.diff.lua", null, null, guid),
                new(alias, AssignmentLayer.UiLayer, "Alias.diff.lua", null, null, guid),
                new(other, AssignmentLayer.Module, "Other.diff.lua", null, null, Guid.NewGuid())
            ], [new(AssignmentLayer.Module, one, "MOD", "JOY_BTN7", "MOD", ModifierMode.Hold, "")]);
            Assert.Single(ModifierAssignmentReservation.Find(state, one, "JOY_BTN7"));
            Assert.Single(ModifierAssignmentReservation.Find(state, alias, "JOY_BTN7"));
            Assert.Empty(ModifierAssignmentReservation.Find(state, other, "JOY_BTN7"));
        }

        /// <summary>Staged change, removal and discard directly control whether the input is reserved.</summary>
        [Fact]
        public void ChangingRemovingAndRestoringModifierReleasesAndRestoresReservation()
        {
            var id = Guid.NewGuid();
            var initial = new WorkspaceManagement([new(id, AssignmentLayer.Module, "MFD.diff.lua", null, null)], [
                new(AssignmentLayer.Module, id, "MOD", "JOY_BTN7", "MOD", ModifierMode.Hold, "")
            ]);
            Assert.Single(ModifierAssignmentReservation.Find(initial, id, "JOY_BTN7"));
            var moved = initial with { Modifiers = [initial.Modifiers[0] with { Key = "JOY_BTN8" }] };
            Assert.Empty(ModifierAssignmentReservation.Find(moved, id, "JOY_BTN7"));
            Assert.Single(ModifierAssignmentReservation.Find(moved, id, "JOY_BTN8"));
            Assert.Empty(ModifierAssignmentReservation.Find(initial with { Modifiers = [] }, id, "JOY_BTN7"));
            Assert.Single(ModifierAssignmentReservation.Find(initial, id, "JOY_BTN7"));
        }

        /// <summary>An unresolved native modifier is matched only to its uniquely identified imported device.</summary>
        [Fact]
        public void LegacyNativeDeviceUsesUnambiguousMatching()
        {
            var id = Guid.NewGuid();
            var other = Guid.NewGuid();
            var unknown = new WorkspaceModifier(AssignmentLayer.Module, Guid.Empty, "LEGACY",
                "JOY_BTN9", "LEGACY", ModifierMode.Hold, "Unique");
            var state = new WorkspaceManagement([
                new(id, AssignmentLayer.Module, "Unique.diff.lua", null, null),
                new(other, AssignmentLayer.Module, "Other.diff.lua", null, null)
            ], [unknown]);
            Assert.Single(ModifierAssignmentReservation.Find(state, id, "JOY_BTN9"));
            Assert.Empty(ModifierAssignmentReservation.Find(state, other, "JOY_BTN9"));
            var ambiguous = state with { Devices = [.. state.Devices, new(other, AssignmentLayer.UiLayer,
                "Unique.diff.lua", null, null)] };
            Assert.Empty(ModifierAssignmentReservation.Find(ambiguous, id, "JOY_BTN9"));
        }
        #endregion END Public
    }
}
