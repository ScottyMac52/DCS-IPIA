using System.Text;
using System.Text.Json;
using Heimdall.Application;
using Heimdall.Domain;

namespace Heimdall.Dcs
{
    /// <summary>Reads the actual generated Lua payload into an immutable physical test plan.</summary>
    /// <param name="Bindings">Generated bindings with physical identities.</param>
    /// <param name="Modifiers">Generated effective modifier activators.</param>
    /// <param name="Diagnostics">Inputs that cannot be resolved without guessing.</param>
    public sealed record DcsGeneratedInputTestPlan(IReadOnlyList<DcsInputTestBinding> Bindings,
        IReadOnlyList<DcsInputTestModifier> Modifiers, IReadOnlyList<string> Diagnostics)
    {
        #region BEGIN Public
        /// <summary>Tests the generated mappings in reviewed scopes and reports scopes that await physical review.</summary>
        /// <param name="workspace">Current editor snapshot, including pending edits.</param>
        /// <returns>Frozen test plan.</returns>
        public static DcsGeneratedInputTestPlan Create(DcsEditorWorkspaceSnapshot workspace)
        {
            var managed = DcsWorkspaceStructureEditor.Describe(workspace);
            var unreviewed = managed.Devices.Where(x => !DcsModulePhysicalBindingReview.IsReviewed(workspace, x)).ToArray();
            if (unreviewed.Length == 0) return Read(DcsSnapshotLuaPayloadRenderer.Generate(workspace));
            // Keep a scope atomic so an omitted modifier never makes shifted bindings appear as Base.
            var blocked = unreviewed.Select(x => x.Layer).ToHashSet();
            var sources = new Dictionary<string, string>(workspace.Sources ?? new Dictionary<string, string>(), StringComparer.Ordinal)
            {
                [DcsWorkspaceManagement.SourcePath] = JsonSerializer.Serialize(new DcsWorkspaceManagement(
                    managed.Devices.Where(x => !blocked.Contains(x.Layer)).ToArray(),
                    managed.Modifiers.Where(x => !blocked.Contains(x.Layer)).ToArray()))
            };
            if (blocked.Contains(AssignmentLayer.Module)) sources.Remove("config/ipia-axis-cleanup-devices.json");
            var testable = workspace with
            {
                Sources = sources,
                Assignments = workspace.Assignments.Where(x => !blocked.Contains(x.Slot.Layer)).ToArray()
            };
            var plan = Read(DcsSnapshotLuaPayloadRenderer.Generate(testable));
            return plan with { Diagnostics = plan.Diagnostics.Concat(unreviewed.GroupBy(x => x.Layer).Select(scope =>
                $"{scope.Key} mappings not tested: rebind inherited or unlinked controllers in Devices ({string.Join("; ", scope.Select(x => x.Title))}).")).ToArray() };
        }
        /// <summary>Parses generated payload entries without selecting devices by display name.</summary>
        /// <param name="payload">Generated package paths and bytes.</param>
        /// <returns>Native physical bindings and effective modifiers.</returns>
        public static DcsGeneratedInputTestPlan Read(IReadOnlyDictionary<string, byte[]> payload)
        {
            var bindings = new List<DcsInputTestBinding>();
            var modifiers = new List<DcsInputTestModifier>();
            var diagnostics = new List<string>();
            foreach (var (path, bytes) in payload)
            {
                var layer = path.StartsWith("Config/Input/UiLayer/", StringComparison.Ordinal) ? AssignmentLayer.UiLayer : AssignmentLayer.Module;
                var text = Encoding.UTF8.GetString(bytes);
                if (path.EndsWith("/modifiers.lua", StringComparison.Ordinal))
                    foreach (var modifier in DcsLuaParser.ParseModifiers(text))
                    {
                        var guid = modifier.Device.Equals("Keyboard", StringComparison.OrdinalIgnoreCase) ? Guid.Empty :
                            DcsPhysicalIdentity.FromFileName(modifier.Device + ".diff.lua");
                        if (guid is null) diagnostics.Add($"Unresolved {layer} modifier: {modifier.Name} ({modifier.Device}).");
                        else modifiers.Add(new(guid.Value, layer, modifier.Name, modifier.Key, modifier.Mode));
                    }
                else if (path.Contains("/joystick/", StringComparison.Ordinal) && path.EndsWith(".diff.lua", StringComparison.Ordinal))
                {
                    var guid = DcsPhysicalIdentity.FromFileName(Path.GetFileName(path));
                    if (guid is null) diagnostics.Add("No native physical GUID: " + Path.GetFileName(path));
                    else bindings.AddRange(DcsLuaParser.ParseProfile(text, guid.Value).Select(x => new DcsInputTestBinding(guid.Value, layer, x)));
                }
            }
            return new(bindings.ToArray(), modifiers.ToArray(), diagnostics.ToArray());
        }
        #endregion END Public
    }
}
