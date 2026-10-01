using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Rendering;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Exercises the IPI 5.7.1 F-16C Ava profile's simultaneous POV aliases through editing and packaging.</summary>
    public sealed class PovAliasAssignmentTests
    {
        private static readonly string[] Directions = ["D", "L", "R", "U"];
        #region BEGIN Public
        /// <summary>Real scaffold bindings become four controls, render labels and PNGs, and preserve both original spellings.</summary>
        [Fact]
        public void RealAvaProfileImportsAndBuildsWithEquivalentAliases()
        {
            var workspace = Workspace();
            var draft = Draft(workspace);
            Assert.Equal(8, workspace.Assignments.Count);
            Assert.Equal(4, draft.Snapshot.Count);
            Assert.All(draft.Snapshot, x => Assert.StartsWith("JOY_BTN_POV1_", x.Slot.InputKey));
            var page = KneeboardDraftProjection.CreateCombined(workspace, workspace.Diagrams[0], workspace.Assignments, workspace.Labels);
            Assert.Equal(4, page.Labels.Count);
            Assert.Contains(page.Labels["D"], x => x.Text == "TRIM Button - NOSE UP");
            var png = Assert.Single(SnapshotDeviceKneeboardRenderer.Generate(workspace)).Value;
            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png.Take(8));
            var parsed = DcsLuaParser.ParseProfile(ModuleLuaProfileRenderer.Render(workspace, workspace.Diagrams[0].ProfileId), workspace.Diagrams[0].ProfileId);
            Assert.Equal(workspace.Configuration.Bindings.Count, parsed.Count);
            foreach (var original in workspace.Configuration.Bindings)
                Assert.Contains(parsed, x => x.CommandId == original.CommandId && x.InputKey == original.InputKey &&
                    x.Disposition == original.Disposition && x.Reformers.SequenceEqual(original.Reformers));
            Assert.False(draft.HasChanges);
        }

        /// <summary>Either spelling can clear or edit a physical control, with undo and removal history preserved.</summary>
        [Theory]
        [InlineData("JOY_POV1_D")]
        [InlineData("JOY_BTN_POV1_D")]
        public void ClearEditAndUndoUpdateBothExportAliases(string key)
        {
            var workspace = Workspace();
            var draft = Draft(workspace);
            var original = draft.Snapshot.ToArray();
            var slot = original.Single(x => x.Slot.InputKey == "JOY_BTN_POV1_D").Slot with { InputKey = key };
            Assert.Equal(DraftOutcome.Applied, draft.Remove(slot).Outcome);
            var profile = slot.ProfileId;
            var cleared = DcsLuaParser.ParseProfile(ModuleLuaProfileRenderer.Render(workspace with { Assignments = draft.Snapshot }, profile), profile);
            Assert.DoesNotContain(cleared, x => x.Disposition != BindingDisposition.Removed && DcsInputKey.SamePhysicalInput(x.InputKey, key));
            Assert.Equal(workspace.Configuration.Bindings.Count(x => x.Disposition == BindingDisposition.Removed), cleared.Count(x => x.Disposition == BindingDisposition.Removed));
            Assert.Equal(DraftOutcome.Applied, draft.Undo().Outcome);
            Assert.Equal(original, draft.Snapshot);
            var replacement = new DraftCommand("NEW", "Replacement", false, AssignmentLayer.Module);
            draft.AddCommands([replacement]);
            Assert.Equal(DraftOutcome.Applied, draft.Assign(slot, replacement, OccupiedDestinationBehavior.ReplaceInDraft).Outcome);
            Assert.Equal(4, draft.Snapshot.Count);
            var edited = DcsLuaParser.ParseProfile(ModuleLuaProfileRenderer.Render(workspace with { Assignments = draft.Snapshot }, profile), profile);
            var aliases = edited.Where(x => x.Disposition != BindingDisposition.Removed && DcsInputKey.SamePhysicalInput(x.InputKey, key)).ToArray();
            Assert.Equal(2, aliases.Length);
            Assert.All(aliases, x => Assert.Equal("NEW", x.CommandId));
            Assert.Equal(DraftOutcome.Applied, draft.Restore(slot).Outcome);
            Assert.False(draft.HasChanges);
            Assert.Equal(original, draft.Snapshot);
            Assert.Equal(DraftOutcome.NoChange, draft.Move(slot, slot with { InputKey = key == "JOY_POV1_D" ? "JOY_BTN_POV1_D" : "JOY_POV1_D" }, OccupiedDestinationBehavior.ReplaceInDraft).Outcome);
        }

        /// <summary>Alias matching preserves device, scope, section, chord, and existing saved-label identities.</summary>
        [Fact]
        public void AliasComparisonRetainsContextAndSavedLabels()
        {
            var workspace = Workspace();
            var original = workspace.Assignments.First(x => x.Slot.InputKey == "JOY_POV1_D");
            var variants = new[] { original, original with { Slot = original.Slot with { ProfileId = Guid.NewGuid() } },
                original with { Slot = original.Slot with { Layer = AssignmentLayer.UiLayer } },
                original with { Slot = original.Slot with { Section = BindingSection.Axis } },
                original with { Slot = original.Slot with { ModifierChord = "SHIFT" } } };
            Assert.Equal(variants.Length, DcsInputKey.CollapseAliases(variants).Count);
            var saved = workspace with { Assignments = [original], Labels = new Dictionary<string, string> { [original.Slot.Key] = "Saved POV label" } };
            var page = KneeboardDraftProjection.CreateCombined(saved, saved.Diagrams[0], saved.Assignments, saved.Labels);
            Assert.Equal("Saved POV label", Assert.Single(page.Labels["D"]).Text);
            Assert.Equal("Saved POV label", KneeboardDraftProjection.Create(saved, saved.Diagrams[0], saved.Assignments, saved.Labels)[0].Captions["D"]);
            Assert.Single(SnapshotDeviceKneeboardRenderer.Generate(saved));
        }

        /// <summary>Aliases with genuinely different commands or tuning remain explicit conflicts.</summary>
        [Fact]
        public void ConflictingAliasesCannotProduceAmbiguousExports()
        {
            var workspace = Workspace();
            var first = workspace.Assignments.First(x => x.Slot.InputKey == "JOY_POV1_D");
            var second = first with { Slot = first.Slot with { InputKey = "JOY_BTN_POV1_D" }, Command = first.Command with { Id = "CONFLICT" } };
            var conflicting = workspace with { Assignments = [first, second] };
            Assert.Throws<ArgumentException>(() => Draft(conflicting));
            Assert.Throws<InvalidDataException>(() => SnapshotDeviceKneeboardRenderer.Generate(conflicting));
            Assert.Throws<InvalidDataException>(() => ModuleLuaProfileRenderer.Render(conflicting, first.Slot.ProfileId));
            var changedBinding = workspace.Configuration.Bindings.Select(x => x.InputKey == "JOY_POV1_D" && x.Disposition != BindingDisposition.Removed
                ? new DcsBinding(x.ProfileId, x.Section, "CONFLICT", x.CommandName, x.Disposition, x.InputKey, x.Reformers, x.Filter) : x).ToArray();
            var config = new ModuleConfiguration(workspace.Configuration.Module, workspace.Configuration.Profiles, workspace.Configuration.Modifiers, changedBinding, "fixture");
            Assert.Throws<InvalidDataException>(() => ModuleLuaProfileRenderer.Render(workspace with { Configuration = config, Assignments = Draft(workspace).Snapshot }, first.Slot.ProfileId));
            second = first with { Slot = first.Slot with { InputKey = "JOY_BTN_POV1_D" }, Filter = new AxisFilter(Deadzone: 0.1) };
            Assert.Equal(2, DcsInputKey.CollapseAliases([first, second]).Count);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static DraftAssignmentService Draft(EditorWorkspaceSnapshot workspace) => new(workspace.Configuration,
            workspace.Assignments.Select(x => x.Command).DistinctBy(x => x.Id), workspace.Assignments);
        private static EditorWorkspaceSnapshot Workspace()
        {
            var module = new DcsModule(Guid.NewGuid(), "F-16C_50", "F-16C Viper");
            var profile = new DeviceProfile(Guid.NewGuid(), "ava-base-f16c", null, "Ava", "Ava.diff.lua");
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "F16", "Ava.diff.lua"));
            var bindings = DcsLuaParser.ParseProfile(source, profile.Id);
            var modifiers = DcsLuaParser.ParseModifiers(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "F16", "modifiers.lua")));
            var config = new ModuleConfiguration(module, [profile], modifiers, bindings, "IPI 5.7.1");
            var controls = Directions.Select((direction, index) =>
                new HardwareGraphicControl(direction, "POV " + direction, "JOY_BTN_POV1_" + direction, 0, index * 20, 20, 10)).ToArray();
            var diagram = new UiLayerDiagram("Ava", null, "ava.svg", 100, 100, [], []) {
                ProfileId = profile.Id, Controls = controls, SupportsUiLayer = false,
                Svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><rect width='100' height='100' fill='black'/><text id='lbl-D' x='20' y='20' fill='white'>Old</text></svg>"
            };
            var assignments = bindings.Where(x => x.Disposition != BindingDisposition.Removed &&
                controls.Any(c => DcsInputKey.SamePhysicalInput(c.InputKey, x.InputKey))).Select(x =>
                new DraftAssignment(new(module.Id, profile.Id, AssignmentLayer.Module, x.Section, x.InputKey, string.Join("+", x.Reformers)),
                    new(x.CommandId, x.CommandName, false, AssignmentLayer.Module))).ToArray();
            var page = new KneeboardPage(Guid.NewGuid(), module.Id, Guid.NewGuid(), KneeboardPageKind.Device, "AVA", "Ava", [], 1);
            return new(config, [page], [], [diagram], [], [], assignments, new Dictionary<string, string>(),
                new Dictionary<string, string> { ["config/kneeboard.json"] = """{"profiles":{"ava":"Ava.diff.lua"},"pages":[{"profile":"ava","file":"AVA"}]}""" });
        }
        #endregion END private helpers
    }
}
