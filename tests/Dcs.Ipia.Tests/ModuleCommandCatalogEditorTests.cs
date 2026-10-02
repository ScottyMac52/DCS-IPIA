using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Checks aircraft catalog CRUD, assigned-command safeguards and module/UI ownership.</summary>
    public sealed class ModuleCommandCatalogEditorTests
    {
        #region BEGIN Public
        /// <summary>Renaming refreshes aircraft bindings and drafts while preserving labels and UI commands.</summary>
        [Fact]
        public void Edits_preserve_scopes_labels_and_imported_binding_history()
        {
            var module = new DcsModule(Guid.NewGuid(), "TEST", "Test");
            var profile = new DeviceProfile(Guid.NewGuid(), "test", null, "Test", "Test.diff.lua");
            var diagram = new UiLayerDiagram("Test", null, "test.svg", 100, 100, [], [])
            {
                ProfileId = profile.Id,
                Controls = [new HardwareGraphicControl("button", "Button", "JOY_BTN1", 1, 1, 10, 10)]
            };
            var slot = new DraftAssignmentSlot(module.Id, profile.Id, AssignmentLayer.Module, BindingSection.Key, "JOY_BTN1");
            var command = new CommandPaletteEntry("FIRE", "Old name", "Weapons", false);
            var configuration = new ModuleConfiguration(module, [profile], [], [
                new(profile.Id, BindingSection.Key, "FIRE", "Old name", BindingDisposition.Added, "JOY_BTN1"),
                new(profile.Id, BindingSection.Key, "UNMAPPED", "Unmapped", BindingDisposition.Added, "JOY_BTN999"),
                new(profile.Id, BindingSection.Key, "HISTORY", "History", BindingDisposition.Removed, "JOY_BTN2")], "fixture");
            var workspace = new EditorWorkspaceSnapshot(configuration, [], [], [diagram], [command], [], [
                new(slot, new("FIRE", "Old name", false, AssignmentLayer.Module)),
                new(slot with { Layer = AssignmentLayer.UiLayer }, new("FIRE", "UI name", false, AssignmentLayer.UiLayer))],
                new Dictionary<string, string> { [slot.Key] = "Custom label" }, new Dictionary<string, string> { ["unrelated"] = "keep" });
            var renamed = ModuleCommandCatalogEditor.Set(workspace, command, command with { Name = "Fire weapon", Category = "Armament" });
            Assert.Equal("Fire weapon", renamed.Assignments.Single(x => x.Slot.Layer == AssignmentLayer.Module).Command.Name);
            Assert.Equal("UI name", renamed.Assignments.Single(x => x.Slot.Layer == AssignmentLayer.UiLayer).Command.Name);
            Assert.Equal("Custom label", renamed.Labels[slot.Key]);
            Assert.Equal("keep", renamed.Sources!["unrelated"]);
            Assert.Equal("History", renamed.Configuration.Bindings.Single(x => x.CommandId == "HISTORY").CommandName);
            Assert.Throws<InvalidOperationException>(() => ModuleCommandCatalogEditor.Remove(workspace, command));
            Assert.Throws<InvalidOperationException>(() => ModuleCommandCatalogEditor.Set(workspace, command, command with { IsAxis = true }));
            Assert.Throws<InvalidOperationException>(() => ModuleCommandCatalogEditor.Set(workspace, command, command with { Id = "OTHER" }));
            Assert.Throws<InvalidOperationException>(() => ModuleCommandCatalogEditor.Set(workspace, command with { Name = "Stale" }, command));
            var unmapped = new CommandPaletteEntry("UNMAPPED", "Unmapped", "Keys", false);
            var extended = ModuleCommandCatalogEditor.Set(workspace, null, unmapped);
            Assert.Throws<InvalidOperationException>(() => ModuleCommandCatalogEditor.Remove(extended, unmapped));
            var cleared = workspace with { Assignments = workspace.Assignments.Where(x => x.Slot.Layer != AssignmentLayer.Module).ToArray() };
            var removed = ModuleCommandCatalogEditor.Remove(cleared, command);
            Assert.Empty(removed.ModuleCommands);
            Assert.Single(removed.Assignments);
            Assert.Equal(3, removed.Configuration.Bindings.Count);
        }

        /// <summary>Adds and updates unused key and axis commands, rejecting invalid and duplicate codes.</summary>
        [Fact]
        public void Validates_catalog_and_allows_unused_axis_changes()
        {
            var configuration = new ModuleConfiguration(new(Guid.NewGuid(), "TEST", "Test"), [], [], [], "fixture");
            var workspace = new EditorWorkspaceSnapshot(configuration, [], [], [], [], [], [], new Dictionary<string, string>());
            var key = new CommandPaletteEntry("KEY", "Action", "Keys", false);
            var axis = new CommandPaletteEntry("AXIS", "Pitch", "Axes", true);
            var first = ModuleCommandCatalogEditor.Set(workspace, null, key);
            var both = ModuleCommandCatalogEditor.Set(first, null, axis);
            Assert.Equal(2, both.ModuleCommands.Count);
            Assert.Throws<InvalidOperationException>(() => ModuleCommandCatalogEditor.Set(both, null, key));
            Assert.Throws<InvalidOperationException>(() => ModuleCommandCatalogEditor.Set(workspace, null, key with { Id = "Bad code" }));
            Assert.Throws<InvalidOperationException>(() => ModuleCommandCatalogEditor.Set(workspace, null, key with { Name = "" }));
            Assert.Throws<InvalidOperationException>(() => ModuleCommandCatalogEditor.Set(workspace, null, key with { Category = " Bad" }));
            var changed = ModuleCommandCatalogEditor.Set(both, axis, axis with { IsAxis = false });
            Assert.False(changed.ModuleCommands.Single(x => x.Id == "AXIS").IsAxis);
            Assert.Single(ModuleCommandCatalogEditor.Remove(changed, key).ModuleCommands);
            Assert.Throws<InvalidOperationException>(() => ModuleCommandCatalogEditor.Remove(workspace, key));
        }
        #endregion END Public
    }
}
