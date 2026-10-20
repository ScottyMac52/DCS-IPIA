using System.Text;
using Heimdall.Application;
using Heimdall.Domain;

namespace Heimdall.Dcs
{
    /// <summary>Reads the actual generated Lua payload into an immutable physical test plan.</summary>
    /// <param name="Bindings">Generated bindings with physical identities.</param>
    /// <param name="Modifiers">Generated effective modifier activators.</param>
    /// <param name="Diagnostics">Inputs that cannot be resolved without guessing.</param>
    public sealed record GeneratedInputTestPlan(IReadOnlyList<InputTestBinding> Bindings,
        IReadOnlyList<InputTestModifier> Modifiers, IReadOnlyList<string> Diagnostics)
    {
        #region BEGIN Public
        /// <summary>Generates the same Lua used by packaging before preparing a test.</summary>
        /// <param name="workspace">Current editor snapshot, including pending edits.</param>
        /// <returns>Frozen test plan.</returns>
        public static GeneratedInputTestPlan Create(EditorWorkspaceSnapshot workspace) => Read(SnapshotLuaPayloadRenderer.Generate(workspace));
        /// <summary>Parses generated payload entries without selecting devices by display name.</summary>
        /// <param name="payload">Generated package paths and bytes.</param>
        /// <returns>Native physical bindings and effective modifiers.</returns>
        public static GeneratedInputTestPlan Read(IReadOnlyDictionary<string, byte[]> payload)
        {
            var bindings = new List<InputTestBinding>();
            var modifiers = new List<InputTestModifier>();
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
                    else bindings.AddRange(DcsLuaParser.ParseProfile(text, guid.Value).Select(x => new InputTestBinding(guid.Value, layer, x)));
                }
            }
            return new(bindings.ToArray(), modifiers.ToArray(), diagnostics.ToArray());
        }
        #endregion END Public
    }
}
