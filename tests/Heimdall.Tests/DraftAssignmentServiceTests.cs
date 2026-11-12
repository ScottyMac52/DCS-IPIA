using Heimdall.Application;
using Heimdall.Domain;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Checks atomic in-memory editing, ownership, conflicts, and undo.</summary>
    public sealed class DraftAssignmentServiceTests
    {
        #region BEGIN Public
        /// <summary>Observers see one complete atomic mutation and the resulting dirty state, including undo.</summary>
        [Theory]
        [InlineData("assign")]
        [InlineData("remove")]
        [InlineData("move")]
        [InlineData("copy")]
        [InlineData("swap")]
        public void Applied_mutations_notify_once_after_state_and_history_are_updated(string operation)
        {
            var (service, source, target, _, first, second) = Fixture();
            var observed = new List<(bool Dirty, int History, DcsDraftAssignment[] Assignments)>();
            service.Changed += (_, _) => observed.Add((service.HasChanges, service.History.Count, service.Snapshot.ToArray()));
            var result = operation switch
            {
                "assign" => service.Assign(source, second, OccupiedDestinationBehavior.Ask, DraftConflictChoice.Replace),
                "remove" => service.Remove(source),
                "move" => service.Move(source, target, OccupiedDestinationBehavior.Ask, DraftConflictChoice.Replace),
                "copy" => service.Copy(source, target, OccupiedDestinationBehavior.ReplaceInDraft),
                _ => service.Swap(source, target)
            };
            Assert.Equal(DraftOutcome.Applied, result.Outcome);
            var change = Assert.Single(observed);
            Assert.True(change.Dirty);
            Assert.Equal(1, change.History);
            Assert.Equal(service.Snapshot, change.Assignments);
            Assert.Equal(DraftOutcome.Applied, service.Undo().Outcome);
            Assert.Equal(2, observed.Count);
            Assert.False(observed[1].Dirty);
            Assert.Equal(0, observed[1].History);
            Assert.Equal(first, Assert.Single(observed[1].Assignments, x => x.Slot == source).Command);
        }

        /// <summary>Rejected, cancelled, unconfirmed and unchanged edits never notify observers.</summary>
        [Fact]
        public void Unapplied_operations_do_not_notify()
        {
            var (service, source, target, other, first, _) = Fixture();
            var notifications = 0;
            service.Changed += (_, _) => notifications++;
            service.Assign(source, first, OccupiedDestinationBehavior.Ask);
            service.Move(source, target, OccupiedDestinationBehavior.Ask);
            service.Move(source, target, OccupiedDestinationBehavior.Ask, DraftConflictChoice.Cancel);
            service.Copy(source, target, OccupiedDestinationBehavior.Reject);
            service.Move(source, other, OccupiedDestinationBehavior.ReplaceInDraft);
            service.Remove(source with { InputKey = "JOY_BTN99" });
            service.Restore(source);
            service.Undo();
            service.Discard();
            Assert.Equal(0, notifications);
            Assert.False(service.HasChanges);
            Assert.Empty(service.History);
        }

        /// <summary>Axis tuning and restoring or discarding edits publish only actual assignment changes.</summary>
        [Fact]
        public void Tuning_restore_and_discard_notify_resulting_state()
        {
            var (_, source, _, _, _, _) = Fixture();
            var axisSlot = source with { Section = BindingSection.Axis, InputKey = "JOY_X" };
            var axis = new DcsDraftCommand("PITCH", "Pitch", true, AssignmentLayer.Module);
            var config = BuildModule(source.ModuleId, source.ProfileId, Guid.NewGuid());
            var service = new DcsDraftAssignmentService(config, [axis], [new(axisSlot, axis)]);
            var dirty = new List<bool>();
            service.Changed += (_, _) => dirty.Add(service.HasChanges);
            var filter = new AxisFilter(5);
            service.TuneAxis(axisSlot, filter);
            service.TuneAxis(axisSlot, new AxisFilter(5));
            service.TuneAxis(axisSlot, new AxisFilter(double.NaN));
            Assert.True(Assert.Single(dirty));
            service.Restore(axisSlot);
            Assert.Collection(dirty, value => Assert.True(value), value => Assert.False(value));
            service.TuneAxis(axisSlot, filter);
            service.Discard();
            service.Discard();
            Assert.Collection(dirty, value => Assert.True(value), value => Assert.False(value),
                value => Assert.True(value), value => Assert.False(value));
            Assert.Empty(service.History);
            Assert.Null(Assert.Single(service.Snapshot).Filter);
        }

        /// <summary>A newly imported module command becomes assignable without resetting the pending draft.</summary>
        [Fact]
        public void Imported_catalog_command_is_assignable_without_losing_history()
        {
            var (service, source, _, _, _, _) = Fixture();
            var incoming = new DcsDraftCommand("NEW_COMMAND", "New module command", false, AssignmentLayer.Module);
            service.AddCommands([incoming]);
            var empty = source with { InputKey = "JOY_BTN9" };
            Assert.Equal(DraftOutcome.Applied,
                         service.Assign(empty, incoming, OccupiedDestinationBehavior.Ask).Outcome);
            Assert.Single(service.History);
            Assert.Equal(incoming, Assert.Single(service.Snapshot, x => x.Slot == empty).Command);
            Assert.Throws<ArgumentException>(() => service.AddCommands([incoming with { IsAxis = true }]));
            Assert.Single(service.History);
        }
        /// <summary>Ask requires an explicit choice, and Cancel leaves the whole draft intact.</summary>
        [Fact]
        public void Ask_conflict_requires_choice_without_mutating()
        {
            var (service, source, target, _, _, _) = Fixture();
            var prior = service.Snapshot.ToArray();
            Assert.Equal(DraftOutcome.ConfirmationRequired,
                         service.Move(source, target, OccupiedDestinationBehavior.Ask).Outcome);
            Assert.Equal(
                DraftOutcome.Blocked,
                service.Move(source, target, OccupiedDestinationBehavior.Ask, DraftConflictChoice.Cancel).Outcome);
            Assert.Equal(prior, service.Snapshot);
            Assert.Empty(service.History);
        }
        /// <summary>Replace stages both ends as one operation and Undo restores them together.</summary>
        [Fact]
        public void Move_replace_is_atomic_and_undoable()
        {
            foreach (var (behavior, choice)
                         in new[] { (OccupiedDestinationBehavior.Ask,
                                     (DraftConflictChoice?)DraftConflictChoice.Replace),
                                    (OccupiedDestinationBehavior.ReplaceInDraft, (DraftConflictChoice?)null) })
            {
                var (service, source, target, _, first, _) = Fixture();
                var original = service.Snapshot.ToArray();
                var result = service.Move(source, target, behavior, choice);
                Assert.Equal(DraftOutcome.Applied, result.Outcome);
                Assert.Equal(2, result.Operation!.Changes.Count);
                Assert.DoesNotContain(service.Snapshot, x => x.Slot == source);
                Assert.Equal(first, Assert.Single(service.Snapshot, x => x.Slot == target).Command);
                Assert.True(service.HasChanges);
                Assert.Equal(DraftOutcome.Applied, service.Undo().Outcome);
                Assert.Equal(original, service.Snapshot);
                Assert.False(service.HasChanges);
            }
        }
        /// <summary>Swap stages two assignments and survives undo without touching another instance.</summary>
        [Fact]
        public void Explicit_swap_and_ask_swap_keep_context()
        {
            var (service, source, target, other, first, second) = Fixture();
            Assert.Equal(
                DraftOutcome.Applied,
                service.Move(source, target, OccupiedDestinationBehavior.Ask, DraftConflictChoice.Swap).Outcome);
            Assert.Equal(second, Assert.Single(service.Snapshot, x => x.Slot == source).Command);
            Assert.Equal(first, Assert.Single(service.Snapshot, x => x.Slot == target).Command);
            Assert.Equal(DraftOutcome.Blocked, service.Swap(source, other).Outcome);
            Assert.Equal(DraftOutcome.Applied, service.Undo().Outcome);
            Assert.Equal(DraftOutcome.Applied, service.Swap(source, target).Outcome);
            Assert.Equal(second, Assert.Single(service.Snapshot, x => x.Slot == source).Command);
            Assert.Equal(first, Assert.Single(service.Snapshot, x => x.Slot == target).Command);
        }
        /// <summary>Copy leaves the original intact; a blocked drop cannot mutate the draft.</summary>
        [Fact]
        public void Copy_and_reject_respect_occupied_targets()
        {
            var (service, source, target, _, first, _) = Fixture();
            Assert.Equal(DraftOutcome.Blocked,
                         service.Copy(source, target, OccupiedDestinationBehavior.Reject).Outcome);
            Assert.False(service.HasChanges);
            Assert.Equal(
                DraftOutcome.Blocked,
                service.Copy(source, target, OccupiedDestinationBehavior.Ask, DraftConflictChoice.Swap).Outcome);
            Assert.Equal(DraftOutcome.Applied,
                         service.Copy(source, target, OccupiedDestinationBehavior.ReplaceInDraft).Outcome);
            Assert.Equal(first, Assert.Single(service.Snapshot, x => x.Slot == source).Command);
            Assert.Equal(first, Assert.Single(service.Snapshot, x => x.Slot == target).Command);
            Assert.Single(Assert.Single(service.History).Changes);
            Assert.Equal(DraftOutcome.Applied, service.Undo().Outcome);
            Assert.False(service.HasChanges);
        }
        /// <summary>Assign and remove/restore change only the selected input and can be discarded.</summary>
        [Fact]
        public void Assign_remove_restore_and_discard()
        {
            var (service, source, target, _, first, second) = Fixture();
            var baseEmpty = source with { InputKey = "JOY_BTN9" };
            Assert.Equal(DraftOutcome.Applied,
                         service.Assign(baseEmpty, first, OccupiedDestinationBehavior.Ask).Outcome);
            Assert.Equal(DraftOutcome.NoChange,
                         service.Assign(baseEmpty, first, OccupiedDestinationBehavior.Ask).Outcome);
            Assert.Equal(DraftOutcome.Blocked,
                         service.Assign(target, first, OccupiedDestinationBehavior.Reject).Outcome);
            Assert.Equal(DraftOutcome.Applied, service.Remove(source).Outcome);
            Assert.Equal(DraftOutcome.NoChange, service.Remove(source).Outcome);
            Assert.Equal(DraftOutcome.Applied, service.Restore(source).Outcome);
            Assert.Equal(first, Assert.Single(service.Snapshot, x => x.Slot == source).Command);
            Assert.Equal(DraftOutcome.Applied, service.Remove(target).Outcome);
            Assert.Equal(DraftOutcome.Applied, service.Restore(target).Outcome);
            service.Discard();
            Assert.False(service.HasChanges);
            Assert.Empty(service.History);
            Assert.Equal(DraftOutcome.NoChange, service.Undo().Outcome);
        }
        /// <summary>Unsupported commands, cross-module targets, axes, modifiers, and instances are rejected.</summary>
        [Fact]
        public void Invalid_scope_or_context_never_changes_draft()
        {
            var (service, source, target, other, first, _) = Fixture();
            var invalidModule = target with { ModuleId = Guid.NewGuid() };
            var invalidProfile = target with { ProfileId = Guid.NewGuid() };
            var uiTarget = target with { Layer = AssignmentLayer.UiLayer };
            Assert.Equal(DraftOutcome.Blocked,
                         service.Move(source, other, OccupiedDestinationBehavior.ReplaceInDraft).Outcome);
            Assert.Equal(DraftOutcome.Blocked,
                         service.Move(source, uiTarget, OccupiedDestinationBehavior.ReplaceInDraft).Outcome);
            Assert.Equal(DraftOutcome.Blocked,
                         service.Assign(invalidModule, first, OccupiedDestinationBehavior.Ask).Outcome);
            Assert.Equal(DraftOutcome.Blocked,
                         service.Assign(invalidProfile, first, OccupiedDestinationBehavior.Ask).Outcome);
            Assert.Equal(
                DraftOutcome.Blocked,
                service.Assign(target with { Section = BindingSection.Axis }, first, OccupiedDestinationBehavior.Ask)
                    .Outcome);
            Assert.Equal(
                DraftOutcome.Blocked,
                service.Assign(target with { ModifierChord = "UNKNOWN" }, first, OccupiedDestinationBehavior.Ask)
                    .Outcome);
            Assert.Equal(
                DraftOutcome.Blocked,
                service.Assign(target, first with { Layer = AssignmentLayer.UiLayer }, OccupiedDestinationBehavior.Ask)
                    .Outcome);
            Assert.Equal(
                DraftOutcome.Blocked,
                service.Assign(target, first with { Id = "fabricated" }, OccupiedDestinationBehavior.Ask).Outcome);
            Assert.False(service.HasChanges);
        }
        /// <summary>UI Layer assignments remain module-owned and are validated against only UI commands.</summary>
        [Fact]
        public void Ui_layer_is_separate_and_modifier_chords_are_explicit()
        {
            var (service, source, _, _, first, _) = Fixture();
            var uiSlot = source with { Layer = AssignmentLayer.UiLayer, ModifierChord = "S3" };
            var ui = new DcsDraftCommand("UI_ZOOM", "VR Zoom", false, AssignmentLayer.UiLayer);
            Assert.Equal(DraftOutcome.Applied, service.Assign(uiSlot, ui, OccupiedDestinationBehavior.Ask).Outcome);
            Assert.Equal(DraftOutcome.Blocked, service.Assign(source, ui, OccupiedDestinationBehavior.Ask).Outcome);
            Assert.Equal(DraftOutcome.Blocked, service.Assign(uiSlot, first, OccupiedDestinationBehavior.Ask).Outcome);
            Assert.Equal(
                DraftOutcome.Blocked,
                service.Assign(uiSlot with { ModifierChord = "S3+S3" }, ui, OccupiedDestinationBehavior.Ask).Outcome);
            Assert.Single(service.Snapshot, x => x.Slot == uiSlot);
        }
        /// <summary>Imported duplicates and commands from an undeclared scope are rejected before editing.</summary>
        [Fact]
        public void Invalid_initial_data_is_rejected()
        {
            var (service, source, _, _, first, _) = Fixture();
            var module = BuildModule(source.ModuleId, source.ProfileId, Guid.NewGuid());
            var assignments = new[] { new DcsDraftAssignment(source, first), new DcsDraftAssignment(source, first) };
            Assert.Throws<ArgumentException>(() => new DcsDraftAssignmentService(module, [first], assignments));
            Assert.Throws<ArgumentException>(() => new DcsDraftAssignmentService(module, [first, first], []));
            Assert.Throws<ArgumentException>(
                () => new DcsDraftAssignmentService(module, [first], [new(source, first with { Id = "missing" })]));
            Assert.NotNull(service);
        }
        /// <summary>Imported display labels may differ from the catalog name while command identity and type stay validated.</summary>
        [Fact]
        public void Imported_callout_label_does_not_invalidate_canonical_command()
        {
            var moduleId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var module = BuildModule(moduleId, profileId, Guid.NewGuid());
            var slot =
                new DcsDraftAssignmentSlot(moduleId, profileId, AssignmentLayer.UiLayer, BindingSection.Key, "JOY_BTN7");
            var canonical = new DcsDraftCommand("UI_COMMAND", "Catalog name", false, AssignmentLayer.UiLayer);
            var labeled = canonical with { Name = "Kneeboard label" };
            var service = new DcsDraftAssignmentService(module, [canonical], [new(slot, labeled)]);
            Assert.Equal(labeled, Assert.Single(service.Snapshot).Command);
            Assert.Equal(
                DraftOutcome.Applied,
                service.Assign(slot with { InputKey = "JOY_BTN8" }, labeled, OccupiedDestinationBehavior.Ask).Outcome);
            Assert.Equal(DraftOutcome.Blocked,
                         service
                             .Assign(slot with { InputKey = "JOY_BTN9" }, labeled with { Id = "OTHER" },
                                     OccupiedDestinationBehavior.Ask)
                             .Outcome);
            Assert.Equal(DraftOutcome.Blocked,
                         service
                             .Assign(slot with { InputKey = "JOY_BTN9" }, labeled with { IsAxis = true },
                                     OccupiedDestinationBehavior.Ask)
                             .Outcome);
        }
        /// <summary>Axis tuning is scoped to one assignment and moves, copies, and undoes with the command.</summary>
        [Fact]
        public void Axis_tuning_stays_with_assignment_and_is_undoable()
        {
            var moduleId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var module = BuildModule(moduleId, profileId, Guid.NewGuid());
            var command = new DcsDraftCommand("PITCH", "Pitch", true, AssignmentLayer.Module);
            var source =
                new DcsDraftAssignmentSlot(moduleId, profileId, AssignmentLayer.Module, BindingSection.Axis, "JOY_X");
            var target = source with { InputKey = "JOY_Y" };
            var initial = new AxisFilter(0.03, 1, 0.8, [0.2], true, false);
            var draft = new DcsDraftAssignmentService(module, [command],
                                                   [new DcsDraftAssignment(source, command) { Filter = initial }]);
            Assert.Equal(initial, Assert.Single(draft.Snapshot).Filter);
            var tuned = new AxisFilter(0.05, 1, 0.9, [0.3], false, true);
            Assert.Equal(DraftOutcome.Applied, draft.TuneAxis(source, tuned).Outcome);
            Assert.Equal(tuned, Assert.Single(draft.Snapshot).Filter);
            Assert.Equal(DraftOutcome.Applied, draft.Copy(source, target, OccupiedDestinationBehavior.Ask).Outcome);
            Assert.Equal(tuned, Assert.Single(draft.Snapshot, x => x.Slot == target).Filter);
            Assert.Equal(DraftOutcome.Applied, draft.Undo().Outcome);
            Assert.Equal(DraftOutcome.Applied, draft.Undo().Outcome);
            Assert.Equal(initial, Assert.Single(draft.Snapshot).Filter);
            Assert.False(draft.HasChanges);
            Assert.Equal(DraftOutcome.Blocked,
                         draft.TuneAxis(source with { Section = BindingSection.Key }, tuned).Outcome);
            Assert.Equal(DraftOutcome.Blocked, draft.TuneAxis(source, new AxisFilter(double.NaN)).Outcome);
        }
        /// <summary>Module and canonical UI Layer modifiers remain independently validated.</summary>
        [Fact]
        public void Distinct_scope_modifiers_keep_their_own_assignments()
        {
            var module = new DcsModule(Guid.NewGuid(), "UH-1H", "Huey");
            var profile = new DcsDeviceProfile(Guid.NewGuid(), "tm-mfd", "MFD3", "MFD", "MFD3.diff.lua");
            var configuration = new DcsModuleConfiguration(
                module, [profile], [new DcsModifier("MOZA_F16_F18_BTN3", "MOZA", "JOY_BTN3", ModifierMode.Hold)], [],
                "import");
            var moduleCommand = new DcsDraftCommand("MODULE", "Module command", false, AssignmentLayer.Module);
            var uiCommand = new DcsDraftCommand("UI", "UI command", false, AssignmentLayer.UiLayer);
            var moduleSlot = new DcsDraftAssignmentSlot(module.Id, profile.Id, AssignmentLayer.Module, BindingSection.Key,
                                                     "JOY_BTN1", "MOZA_F16_F18_BTN3");
            var uiSlot = moduleSlot with { Layer = AssignmentLayer.UiLayer, ModifierChord = "MOZA_MODIFIER_BTN3" };
            var service = new DcsDraftAssignmentService(configuration, [moduleCommand, uiCommand],
                                                     [new(moduleSlot, moduleCommand), new(uiSlot, uiCommand)],
                                                     ["MOZA_MODIFIER_BTN3"]);
            Assert.Equal(2, service.Snapshot.Count);
            Assert.Equal(DraftOutcome.Blocked, service
                                                   .Assign(moduleSlot with { ModifierChord = "MOZA_MODIFIER_BTN3" },
                                                           moduleCommand, OccupiedDestinationBehavior.Ask)
                                                   .Outcome);
            Assert.Equal(DraftOutcome.Blocked, service
                                                   .Assign(uiSlot with { ModifierChord = "MOZA_F16_F18_BTN3" },
                                                           uiCommand, OccupiedDestinationBehavior.Ask)
                                                   .Outcome);
        }
        /// <summary>Persistence suspends every write path without losing assignments, undo history or dirty state.</summary>
        [Fact]
        public void Suspended_draft_blocks_all_mutations_until_the_save_releases_it()
        {
            var (service, source, target, _, first, _) = Fixture();
            service.Remove(source);
            var before = service.Snapshot.ToArray();
            var history = service.History.ToArray();
            var notifications = 0;
            service.Changed += (_, _) => notifications++;
            using (service.SuspendEditing())
            {
                var results = new[]
                {
                    service.Assign(source, first, OccupiedDestinationBehavior.ReplaceInDraft),
                    service.Move(target, source, OccupiedDestinationBehavior.ReplaceInDraft),
                    service.Copy(target, source, OccupiedDestinationBehavior.ReplaceInDraft),
                    service.Swap(source, target), service.TuneAxis(source, null),
                    service.Remove(target), service.Restore(source), service.Undo()
                };
                Assert.All(results, result => Assert.Equal(DraftOutcome.Blocked, result.Outcome));
                Assert.Throws<InvalidOperationException>(() => service.AddCommands([first]));
                Assert.Throws<InvalidOperationException>(service.Discard);
                Assert.Equal(before, service.Snapshot);
                Assert.Equal(history, service.History);
                Assert.True(service.HasChanges);
                Assert.Equal(0, notifications);
            }
            Assert.Equal(DraftOutcome.Applied, service.Undo().Outcome);
            Assert.False(service.HasChanges);
            service.AddCommands([first]);
            service.Discard();
        }

        /// <summary>Nested suspensions cannot unlock each other and a lease can safely be disposed twice.</summary>
        [Fact]
        public void Editing_resumes_only_after_every_save_lease_is_released()
        {
            var (service, source, _, _, _, _) = Fixture();
            var outer = service.SuspendEditing();
            var inner = service.SuspendEditing();
            outer.Dispose(); outer.Dispose();
            Assert.Equal(DraftOutcome.Blocked, service.Remove(source).Outcome);
            inner.Dispose();
            Assert.Equal(DraftOutcome.Applied, service.Remove(source).Outcome);
        }
        /// <summary>Occupied-control preview leaves the native draft and undo history unchanged.</summary>
        [Fact]
        public void Reviewed_assignment_and_move_do_not_stage_the_candidate()
        {
            var (service, source, target, _, first, _) = Fixture();
            var before = service.Snapshot;
            var notifications = 0;
            service.Changed += (_, _) => notifications++;

            Assert.Equal(DraftOutcome.ConfirmationRequired, service.ReviewAssign(target, first, null).Outcome);
            Assert.Equal(DraftOutcome.Applied, service.ReviewAssign(target, first, DraftConflictChoice.Replace).Outcome);
            Assert.Equal(DraftOutcome.ConfirmationRequired, service.ReviewTransfer(source, target, false, null).Outcome);
            Assert.Equal(DraftOutcome.Applied, service.ReviewTransfer(source, target, false, DraftConflictChoice.Swap).Outcome);
            Assert.Equal(DraftOutcome.Blocked, service.ReviewTransfer(source, target, true, DraftConflictChoice.Swap).Outcome);
            Assert.Equal(before, service.Snapshot);
            Assert.Empty(service.History);
            Assert.False(service.HasChanges);
            Assert.Equal(0, notifications);

            Assert.Equal(DraftOutcome.Applied, service.Move(source, target, OccupiedDestinationBehavior.Ask, DraftConflictChoice.Swap).Outcome);
            Assert.Single(service.History);
            Assert.Equal(1, notifications);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static (DcsDraftAssignmentService Service, DcsDraftAssignmentSlot Source, DcsDraftAssignmentSlot Target,
                        DcsDraftAssignmentSlot Other, DcsDraftCommand First, DcsDraftCommand Second) Fixture()
        {
            var moduleId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var otherId = Guid.NewGuid();
            var module = BuildModule(moduleId, profileId, otherId);
            var source =
                new DcsDraftAssignmentSlot(moduleId, profileId, AssignmentLayer.Module, BindingSection.Key, "JOY_BTN13");
            var target = source with { InputKey = "JOY_BTN03" };
            var other = target with { ProfileId = otherId };
            var first = new DcsDraftCommand("MODULE_ZOOM", "Zoom", false, AssignmentLayer.Module);
            var second = new DcsDraftCommand("MODULE_PAUSE", "Pause", false, AssignmentLayer.Module);
            var ui = new DcsDraftCommand("UI_ZOOM", "VR Zoom", false, AssignmentLayer.UiLayer);
            var service =
                new DcsDraftAssignmentService(module, [first, second, ui], [new(source, first), new(target, second)]);
            return (service, source, target, other, first, second);
        }
        private static DcsModuleConfiguration BuildModule(Guid moduleId, Guid profileId, Guid otherId) =>
            new(new DcsModule(moduleId, "UH-1H", "Huey"),
                [
                    new DcsDeviceProfile(profileId, "tm-mfd", "MFD3", "MFD", "MFD3.diff.lua"),
                    new DcsDeviceProfile(otherId, "tm-mfd", "MFD1", "MFD", "MFD1.diff.lua")
                ],
                [new DcsModifier("S3", "tm-mfd", "JOY_BTN5", ModifierMode.Hold)], [], "fixture");
        #endregion END private helpers
    }
}
