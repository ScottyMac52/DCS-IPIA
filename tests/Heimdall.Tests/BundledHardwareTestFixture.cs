using System.Text.Json;
using Heimdall.Dcs;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Surfaces rejected drawing diagnostics before assertions can dump embedded SVG image data.</summary>
    internal static class BundledHardwareTestFixture
    {
        #region BEGIN Public
        /// <summary>Loads the real catalog and reports each rejected drawing by identity and reason.</summary>
        internal static async Task<IReadOnlyDictionary<string, string>> LoadAsync()
        {
            var sources = await ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root);
            var errors = JsonSerializer.Deserialize<string[]>(sources["config/ipia-hardware-diagnostics.json"])!;
            Assert.True(errors.Length == 0, "Bundled hardware drawing errors:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
            return sources;
        }
        #endregion END Public
    }
}
