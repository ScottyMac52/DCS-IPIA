using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Checks that exported diff profiles preserve DCS bindings and reflect saved edits.</summary>
    public sealed class ModuleLuaProfileRendererTests
    {
        private static readonly Guid ModuleId = Guid.Parse("71f005b1-f23f-43da-a41d-025334caf502");
        private static readonly Guid ProfileId = Guid.Parse("70563b2c-7ab1-4dca-9702-564b885d0468");
        #region BEGIN Public
        /// <summary>Retains unmapped and removed bindings while applying a saved command and axis filter.</summary>
        [Fact]
        public void Render_AppliesEditsWithoutDiscardingUnmappedOrRemovedBindings()
        {
            var filter = new AxisFilter(Deadzone: 0.15, Curvature: [0.1, 0.2], Invert: true, Slider: false);
            var original = new[] {
                new DcsBinding(ProfileId, BindingSection.Key, "Old", "Old name", BindingDisposition.Added, "JOY_BTN1"),
                new DcsBinding(ProfileId, BindingSection.Key, "Other", "Other name", BindingDisposition.Changed,
                               "JOY_BTN99"),
                new DcsBinding(ProfileId, BindingSection.Key, "Gone", "Gone name", BindingDisposition.Removed,
                               "JOY_BTN3"),
                new DcsBinding(ProfileId, BindingSection.Axis, "AxisOld", "Axis old", BindingDisposition.Added, "JOY_X")
            };
            var edits =
                new[] { Assigned("New", "New \"name\"", BindingSection.Key, "JOY_BTN1"),
                        Assigned("AxisNew", "Axis new", BindingSection.Axis, "JOY_X") with { Filter = filter } };
            var workspace = Workspace(original, edits);
            var rendered = ModuleLuaProfileRenderer.Render(workspace, ProfileId);
            Assert.Equal(rendered, ModuleLuaProfileRenderer.Render(workspace, ProfileId));
            Assert.EndsWith("return diff\n", rendered);
            var parsed = DcsLuaParser.ParseProfile(rendered, ProfileId);
            Assert.Equal(5, parsed.Count);
            Assert.Contains(parsed, x => x.CommandId == "New" && x.CommandName == "New \"name\"" &&
                                         x.Disposition == BindingDisposition.Added && x.InputKey == "JOY_BTN1");
            Assert.Contains(parsed, x => x.CommandId == "Other" && x.Disposition == BindingDisposition.Changed);
            Assert.Contains(parsed, x => x.CommandId == "Gone" && x.Disposition == BindingDisposition.Removed);
            Assert.Contains(parsed, x => x.CommandId == "AxisOld" && x.Disposition == BindingDisposition.Removed && x.InputKey == "JOY_X");
            var axis = Assert.Single(parsed, x => x.CommandId == "AxisNew");
            Assert.Equal(filter.Deadzone, axis.Filter?.Deadzone);
            Assert.Equal(filter.Curvature, axis.Filter?.Curvature);
            Assert.True(axis.Filter?.Invert);
            Assert.False(axis.Filter?.Slider);
        }

        /// <summary>Cleared mapped slots discard all imported commands rather than blocking output.</summary>
        [Fact]
        public void Render_ClearsAmbiguousExistingBindings()
        {
            var originals = new[] {
                new DcsBinding(ProfileId, BindingSection.Key, "One", "One", BindingDisposition.Added, "JOY_BTN1"),
                new DcsBinding(ProfileId, BindingSection.Key, "Two", "Two", BindingDisposition.Added, "JOY_BTN1")
            };
            var rendered = ModuleLuaProfileRenderer.Render(Workspace(originals, []), ProfileId);
            Assert.Empty(DcsLuaParser.ParseProfile(rendered, ProfileId));
        }

        /// <summary>Saved commands replace conflicting import evidence exactly once and retire old axes.</summary>
        [Theory]
        [InlineData(BindingSection.Key, "JOY_BTN1")]
        [InlineData(BindingSection.Axis, "JOY_X")]
        public void Render_ResolvesImportedConflictsWithSavedAssignment(BindingSection section, string key)
        {
            var originals = new[] {
                new DcsBinding(ProfileId, section, "One", "One", BindingDisposition.Added, key),
                new DcsBinding(ProfileId, section, "Two", "Two", BindingDisposition.Changed, key)
            };
            var parsed = DcsLuaParser.ParseProfile(ModuleLuaProfileRenderer.Render(
                Workspace(originals, [Assigned("New", "New", section, key)]), ProfileId), ProfileId);
            Assert.Equal("New", Assert.Single(parsed, x => x.Disposition != BindingDisposition.Removed).CommandId);
            Assert.Equal(section == BindingSection.Axis ? 2 : 0,
                parsed.Count(x => x.Disposition == BindingDisposition.Removed));
        }

        /// <summary>Identical duplicate evidence produces one effective binding.</summary>
        [Fact]
        public void Render_CollapsesIdenticalImportedDuplicates()
        {
            var binding = new DcsBinding(ProfileId, BindingSection.Key, "One", "One", BindingDisposition.Added, "JOY_BTN1");
            var parsed = DcsLuaParser.ParseProfile(ModuleLuaProfileRenderer.Render(
                Workspace([binding, binding], [Assigned("One", "One", BindingSection.Key, "JOY_BTN1")]), ProfileId), ProfileId);
            Assert.Equal("One", Assert.Single(parsed).CommandId);
        }

        /// <summary>Saved tuning replaces conflicting imported filters without duplicating the axis.</summary>
        [Fact]
        public void Render_UsesSavedFilterWhenImportedFiltersConflict()
        {
            var original = new DcsBinding(ProfileId, BindingSection.Axis, "Axis", "Axis", BindingDisposition.Added,
                "JOY_X", [], new AxisFilter(Deadzone: 0.1));
            var other = new DcsBinding(ProfileId, BindingSection.Axis, "Axis", "Axis", BindingDisposition.Added,
                "JOY_X", [], new AxisFilter(Deadzone: 0.2));
            var edit = Assigned("Axis", "Axis", BindingSection.Axis, "JOY_X") with { Filter = new AxisFilter(Deadzone: 0.3) };
            var parsed = DcsLuaParser.ParseProfile(ModuleLuaProfileRenderer.Render(Workspace([original, other], [edit]), ProfileId), ProfileId);
            Assert.Equal(0.3, Assert.Single(parsed).Filter?.Deadzone);
        }

        /// <summary>Different modifier chords stay independent when reconciling an imported conflict.</summary>
        [Fact]
        public void Render_PreservesIndependentModifierChords()
        {
            var originals = new[] {
                new DcsBinding(ProfileId, BindingSection.Key, "One", "One", BindingDisposition.Added, "JOY_BTN1"),
                new DcsBinding(ProfileId, BindingSection.Key, "Two", "Two", BindingDisposition.Added, "JOY_BTN1"),
                new DcsBinding(ProfileId, BindingSection.Key, "Shifted", "Shifted", BindingDisposition.Added, "JOY_BTN1", ["Shift"])
            };
            var shifted = Assigned("Shifted", "Shifted", BindingSection.Key, "JOY_BTN1");
            shifted = shifted with { Slot = shifted.Slot with { ModifierChord = "Shift" } };
            var parsed = DcsLuaParser.ParseProfile(ModuleLuaProfileRenderer.Render(
                Workspace(originals, [Assigned("New", "New", BindingSection.Key, "JOY_BTN1"), shifted]), ProfileId), ProfileId);
            Assert.Equal(2, parsed.Count);
            Assert.Contains(parsed, x => x.CommandId == "New" && x.Reformers.Count == 0);
            Assert.Equal("Shift", Assert.Single(Assert.Single(parsed, x => x.CommandId == "Shifted").Reformers));
        }

        /// <summary>Cleared axes remove every old command even when imports collide.</summary>
        [Fact]
        public void Render_ClearsConflictingAxesWithExplicitRemovals()
        {
            var originals = new[] {
                new DcsBinding(ProfileId, BindingSection.Axis, "One", "One", BindingDisposition.Added, "JOY_X"),
                new DcsBinding(ProfileId, BindingSection.Axis, "Two", "Two", BindingDisposition.Added, "JOY_X")
            };
            var parsed = DcsLuaParser.ParseProfile(ModuleLuaProfileRenderer.Render(Workspace(originals, []), ProfileId), ProfileId);
            Assert.Equal(2, parsed.Count);
            Assert.All(parsed, x => Assert.Equal(BindingDisposition.Removed, x.Disposition));
        }

        /// <summary>Unmapped button commands remain intact; identical copies are collapsed.</summary>
        [Fact]
        public void Render_PreservesDistinctUnmappedCommands()
        {
            var one = new DcsBinding(ProfileId, BindingSection.Key, "One", "One", BindingDisposition.Added, "JOY_BTN99");
            var two = new DcsBinding(ProfileId, BindingSection.Key, "Two", "Two", BindingDisposition.Added, "JOY_BTN99");
            var parsed = DcsLuaParser.ParseProfile(ModuleLuaProfileRenderer.Render(Workspace([one, one, two], []), ProfileId), ProfileId);
            Assert.Equal(2, parsed.Count);
            Assert.Contains(parsed, x => x.CommandId == "One");
            Assert.Contains(parsed, x => x.CommandId == "Two");
        }

        /// <summary>Actual conflicting saved commands identify the exact input and both commands.</summary>
        [Fact]
        public void Render_ReportsActionableSavedConflict()
        {
            var error = Assert.Throws<InvalidDataException>(() => ModuleLuaProfileRenderer.Render(
                Workspace([], [Assigned("One", "Command One", BindingSection.Key, "JOY_BTN1"),
                    Assigned("Two", "Command Two", BindingSection.Key, "JOY_BTN1")]), ProfileId));
            foreach (var detail in new[] { "UH-1H", "Stick.diff.lua", ProfileId.ToString(), "JOY_BTN1", "modifiers []",
                "Command One [One]", "Command Two [Two]", "save to database" })
                Assert.Contains(detail, error.Message);
        }

        /// <summary>Refuses to export an edited slot without an authored hardware mapping.</summary>
        [Fact]
        public void Render_RejectsUnmappedSavedAssignment()
        {
            var edit = Assigned("New", "New", BindingSection.Key, "JOY_BTN99");
            Assert.Throws<InvalidDataException>(() =>
                                                    ModuleLuaProfileRenderer.Render(Workspace([], [edit]), ProfileId));
        }

        /// <summary>A cleared mapped slot removes its active binding while preserving DCS removal history.</summary>
        [Fact]
        public void Render_ClearsMappedSlotAndPreservesRemoval()
        {
            var originals = new[] {
                new DcsBinding(ProfileId, BindingSection.Key, "One", "One", BindingDisposition.Added, "JOY_BTN1"),
                new DcsBinding(ProfileId, BindingSection.Key, "Two", "Two", BindingDisposition.Removed, "JOY_BTN1")
            };
            var rendered = ModuleLuaProfileRenderer.Render(Workspace(originals, []), ProfileId);
            var parsed = DcsLuaParser.ParseProfile(rendered, ProfileId);
            Assert.Equal("Two", Assert.Single(parsed).CommandId);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static DraftAssignment Assigned(string id, string name, BindingSection section, string key) =>
            new(new(ModuleId, ProfileId, AssignmentLayer.Module, section, key),
                new(id, name, section == BindingSection.Axis, AssignmentLayer.Module));
        private static EditorWorkspaceSnapshot Workspace(IEnumerable<DcsBinding> bindings,
                                                         IReadOnlyList<DraftAssignment> assignments)
        {
            var module = new DcsModule(ModuleId, "UH-1H", "Huey");
            var profile = new DeviceProfile(ProfileId, "Stick", null, "Stick", "Stick.diff.lua");
            var configuration = new ModuleConfiguration(module, [profile], [], bindings, "test");
            var diagram = new UiLayerDiagram("Stick", null, "Stick.svg", 100, 100, [], []) {
                ProfileId = ProfileId,
                Controls =
                    [new("button", "Button 1", "JOY_BTN1", 0, 0, 10, 10), new("axis", "X", "JOY_X", 0, 0, 10, 10, true)]
            };
            return new(configuration, [], [], [diagram], [], [], assignments, new Dictionary<string, string>());
        }
        #endregion END private helpers
    }
}

