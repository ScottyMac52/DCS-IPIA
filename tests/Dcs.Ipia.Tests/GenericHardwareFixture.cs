using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;

namespace Dcs.Ipia.Tests
{
    /// <summary>Provides source-free native controls and deliberately personal defaults for generic authoring tests.</summary>
    internal static class GenericHardwareFixture
    {
        #region BEGIN Public
        /// <summary>Creates sparse real inputs or the supported high-count controller limit.</summary>
        /// <param name="highCount">Whether to include all buttons and four hats.</param>
        /// <returns>Mapped capability evidence.</returns>
        public static HardwareDefinition Definition(bool highCount = false)
        {
            var objects = Enumerable.Range(0, highCount ? 128 : 3).Select(x => new ControllerObject(
                Guid.Parse("a36d02f0-c9f3-11cf-bfc7-444553540000"), 12u | ((uint)x << 8), "Button " + (x + 1))).ToList();
            objects.Add(new(Guid.Parse("a36d02e0-c9f3-11cf-bfc7-444553540000"), 2, "Pitch"));
            objects.Add(new(Guid.Parse("a36d02e1-c9f3-11cf-bfc7-444553540000"), 258, "Roll"));
            objects.AddRange(Enumerable.Range(0, highCount ? 4 : 1).Select(x => new ControllerObject(
                Guid.Parse("a36d02f2-c9f3-11cf-bfc7-444553540000"), 16u | ((uint)x << 8), "POV " + (x + 1))));
            objects.Add(new(Guid.Parse("11111111-1111-1111-1111-111111111111"), 1u | (8u << 8), "Unsupported relative axis"));
            var capabilities = ControllerCapabilityMapper.Map(objects);
            return new(ControllerCapabilityMapper.Fingerprint(capabilities), capabilities);
        }

        /// <summary>Creates a neutral module while retaining the shared command palette.</summary>
        /// <returns>Empty independent workspace.</returns>
        public static EditorWorkspaceSnapshot Workspace() => NewModuleWorkspaceFactory.Create("GENERIC", "Generic aircraft", Defaults(),
            new Dictionary<string, string> { ["config/ipia-hardware.json"] = "[]" }, true);

        /// <summary>Creates a verified baseline with a personal controller modifier to catch silent adoption.</summary>
        /// <returns>Saved default revision.</returns>
        public static SharedUiLayerWorkspace Defaults()
        {
            var sources = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["assets/shared/ui-layer/functions.json"] = """{"functions":[{"id":"pause","command":"d3001pnilu3001cdnilvd1vpnilvunil","label":"Pause","category":"General"}]}""",
                ["assets/shared/ui-layer/input/UiLayer/modifiers.lua"] = "local modifiers = { [\"PERSONAL\"] = { [\"device\"] = \"Personal {11111111-1111-1111-1111-111111111111}\", [\"key\"] = \"JOY_BTN1\", [\"switch\"] = true } } return modifiers"
            };
            var commit = new string('a', 40);
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(commit + "\n" + JsonSerializer.Serialize(sources))));
            return new(1, commit, sources, hash);
        }

        /// <summary>Creates one explicitly adopted repeated physical unit.</summary>
        /// <param name="definition">Shared controls.</param>
        /// <param name="label">Independent physical instance label.</param>
        /// <returns>A distinct physical choice.</returns>
        public static InventoryHardwareChoice Unit(HardwareDefinition definition, string label) =>
            new(new(Guid.NewGuid(), "Unbundled controller", definition.Id, label, "Pilot", true, []), definition);
        #endregion END Public
    }
}
