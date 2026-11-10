using System.IO.Compression;
using Heimdall.Dcs;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects complete DCS authoring data when accepting generated OVGME output for smoke tests.</summary>
    public sealed class DcsPerformancePackageInputTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "heimdall-performance-package-" + Guid.NewGuid().ToString("N"));
        #region BEGIN Public
        /// <summary>A generated package alone cannot pretend to preserve editable drawings and command definitions.</summary>
        [Fact]
        public void Plain_package_requires_its_matching_authoring_snapshot()
        {
            Directory.CreateDirectory(_root);
            var snapshot = DcsModuleSnapshotWorkflow.Freeze(MixedSnapshotFixture.Create(), "1.0.0");
            var package = DcsModuleSnapshotWorkflow.Build(snapshot);
            var path = Path.Combine(_root, package.FileName); File.WriteAllBytes(path, package.Bytes);
            var error = Assert.Throws<InvalidDataException>(() => DcsPerformancePackageInput.Read(path, null));
            Assert.Contains("DcsSnapshotFile", error.Message);
            var companion = Path.Combine(_root, "module.heimdall-module.json");
            DcsModuleSnapshotWorkflow.Write(snapshot, companion);
            Assert.Equal(snapshot, DcsPerformancePackageInput.Read(path, companion));
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
                foreach (var entry in zip.Entries) entry.LastWriteTime = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
            Assert.Equal(snapshot, DcsPerformancePackageInput.Read(path, companion));
            var changed = snapshot with { Version = "2.0.0" };
            var wrong = Path.Combine(_root, "wrong.heimdall-module.json");
            DcsModuleSnapshotWorkflow.Write(changed, wrong);
            Assert.Throws<InvalidDataException>(() => DcsPerformancePackageInput.Read(path, wrong));
        }
        /// <summary>A verified Complete release supplies both generated output and exact editable inputs.</summary>
        [Fact]
        public void Complete_release_restores_portable_build_inputs()
        {
            Directory.CreateDirectory(_root);
            var snapshot = DcsModuleSnapshotWorkflow.Freeze(MixedSnapshotFixture.Create(), "1.0.0");
            var package = DcsModuleCompleteReleaseWorkflow.Build(snapshot);
            var path = Path.Combine(_root, package.FileName); File.WriteAllBytes(path, package.Bytes);
            Assert.Equal(snapshot, DcsPerformancePackageInput.Read(path, null));
            Assert.Throws<InvalidDataException>(() => DcsPerformancePackageInput.Read(path, "unused.json"));
        }
        /// <inheritdoc/>
        public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
        #endregion END Public
    }
}
