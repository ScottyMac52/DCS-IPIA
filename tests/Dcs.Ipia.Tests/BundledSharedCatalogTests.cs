using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Protects packaged shared source integrity and checkout-free hardware authoring.</summary>
    public sealed class BundledSharedCatalogTests
    {
        #region BEGIN Public
        /// <summary>All shipped sources verify and the real shared catalog loads without a Git checkout.</summary>
        [Fact]
        public async Task Loads_packaged_hardware_without_common_checkout()
        {
            Assert.Equal(BundledSharedCatalog.SourceCommit(), BundledSharedCatalog.Validate(BundledSharedCatalog.Root));
            Assert.True(BundledSharedCatalog.IsRoot(BundledSharedCatalog.Root));
            Assert.False(BundledSharedCatalog.IsRoot(Path.GetTempPath()));
            Assert.Equal(12, BundledSharedCatalog.Paths("assets/shared/hardware/drawio").Length);
            var sources = await ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root);
            var hardware = JsonSerializer.Deserialize<WorkspaceHardware[]>(sources["config/ipia-hardware.json"])!;
            Assert.Equal(12, hardware.Select(x => x.Diagram.Path).Distinct().Count());
            Assert.Contains(hardware, x => x.DeviceId == "ava-base-f16c" && x.Diagram.Controls.Count > 0);
            Assert.Contains("assets/shared/ui-layer/input/UiLayer/modifiers.lua", sources.Keys);
            Assert.Contains("config/ipia-shared-assets-provenance.json", sources.Keys);
            Assert.NotEmpty(await BundledSharedCatalog.ReadTextAsync("assets/shared/hardware/manifest.json"));
        }
        /// <summary>Missing, edited, and unsafe bundled inputs produce explicit failures.</summary>
        /// <param name="scenario">Integrity failure to introduce.</param>
        [Theory]
        [InlineData("valid")]
        [InlineData("missing")]
        [InlineData("edited")]
        [InlineData("size")]
        [InlineData("path")]
        [InlineData("duplicate")]
        [InlineData("empty")]
        [InlineData("commit")]
        public void Validates_source_bytes_and_manifest(string scenario)
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-bundle-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                var path = scenario == "path" ? "../outside" : "DCS-Common-LICENSE.txt";
                var entry = new { path, size = scenario == "size" ? 7 : 6, gitBlob = "ce013625030ba8dba906f756967f9e9ca394464a" };
                var files = scenario == "empty" ? Array.Empty<object>() : scenario == "duplicate" ? new object[] { entry, entry } : [entry];
                File.WriteAllText(Path.Combine(root, "provenance.json"), JsonSerializer.Serialize(new {
                    sourceCommit = scenario == "commit" ? "invalid" : new string('a', 40), files
                }));
                if (scenario != "missing") File.WriteAllText(Path.Combine(root, "DCS-Common-LICENSE.txt"), scenario == "edited" ? "world\n" : "hello\n");
                if (scenario == "valid") Assert.Equal(new string('a', 40), BundledSharedCatalog.Validate(root));
                else Assert.Throws<InvalidDataException>(() => BundledSharedCatalog.Validate(root));
            }
            finally { Directory.Delete(root, true); }
        }
        #endregion END Public
        #region BEGIN private helpers
        #endregion END private helpers
    }
}
