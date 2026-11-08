using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.Domain;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Save-accept and discard rebuild the draft with the same catalog the open path validated.</summary>
    public sealed class DcsSavedWorkspaceDraftTests
    {
        private static DcsEditorWorkspaceSnapshot BindingDerived()
        {
            // Shape of the command-conformance fixture: the stored module catalog is empty and the assigned
            // command is known only from the module device's bindings, which the open path accepts.
            var module = Guid.NewGuid(); var profile = Guid.NewGuid();
            var device = new DcsDeviceProfile(profile, "stick", null, "Stick", "Stick {00000000-0000-0000-0000-000000000001}.diff.lua");
            var bindings = new DcsBinding[] { new(profile, BindingSection.Axis, "a2001cdnil", "Pitch", BindingDisposition.Added, "JOY_Y") };
            var assignment = new DcsDraftAssignment(new(module, profile, AssignmentLayer.Module, BindingSection.Axis, "JOY_Y"), new("a2001cdnil", "Pitch", true, AssignmentLayer.Module));
            return new(new(new DcsModule(module, "DRAFT-TEST", "Draft test"), [device], [], bindings, "test"), [], [], [], [], [], [assignment],
                new Dictionary<string, string>(), new Dictionary<string, string>());
        }

        [Fact]
        public void Binding_derived_saved_assignments_rebuild_with_identity_and_layer_intact()
        {
            var saved = BindingDerived();
            var draft = DcsSavedWorkspaceDraft.Create(saved);
            Assert.Equal(saved.Assignments, draft.Snapshot);
            Assert.False(draft.HasChanges);
            Assert.Contains(DcsSavedWorkspaceDraft.ModuleCatalog(saved), x => x.Id == "a2001cdnil" && x.IsAxis);
        }

        [Fact]
        public void Unknown_commands_are_still_rejected()
        {
            var saved = BindingDerived();
            var stray = saved with { Assignments = [new(saved.Assignments[0].Slot, new("a9999cdnil", "Stray", true, AssignmentLayer.UiLayer))] };
            Assert.Throws<ArgumentException>(() => DcsSavedWorkspaceDraft.Create(stray));
        }
    }
}
